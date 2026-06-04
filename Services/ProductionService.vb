Imports System.Data.SQLite
Imports System.Collections.Generic

''' <summary>
''' 생산 서비스 (핵심 비즈니스 로직)
''' 백플러시 트랜잭션, Poka-Yoke, 공정 시퀀스 제어, 설비 인터락 통합
''' </summary>
Public Class ProductionService

    Private ReadOnly _bomRepo As New BomRepository()
    Private ReadOnly _stockRepo As New StockRepository()
    Private ReadOnly _prodRepo As New ProductionRepository()
    Private ReadOnly _lotRepo As New LotTraceRepository()
    Private ReadOnly _equipService As New EquipmentService()

    ''' <summary>
    ''' 공정 시퀀스 정의 (자동차 제조 표준 흐름)
    ''' 프레스 -> 용접 -> 도장 -> 조립 -> 검사
    ''' </summary>
    Private Shared ReadOnly ProcessSequence As ProcessStep() = {
        ProcessStep.Press,
        ProcessStep.Welding,
        ProcessStep.Painting,
        ProcessStep.Assembly,
        ProcessStep.Inspection
    }

    ''' <summary>
    ''' 단건 생산 실행 (백플러시 + 인터락 + LOT 추적 통합)
    ''' </summary>
    ''' <param name="itemCd">완제품 코드</param>
    ''' <param name="prodQty">생산 수량</param>
    ''' <param name="lineCd">생산 라인 코드</param>
    ''' <param name="logger">로그 출력 콜백</param>
    ''' <returns>성공 시 True, 실패 시 False</returns>
    Public Function ExecuteProduction(itemCd As String, prodQty As Double, lineCd As String, logger As Action(Of String)) As Boolean
        If String.IsNullOrWhiteSpace(itemCd) Then
            Throw New ErpException("완제품 코드가 지정되지 않았습니다.", "PROD-001")
        End If
        If prodQty <= 0 Then
            Throw New ErpException("생산 수량은 0보다 커야 합니다.", "PROD-002")
        End If

        ' LOT 및 바코드 생성
        Dim lotNo As String = BarcodeService.GenerateLotNo(If(String.IsNullOrEmpty(lineCd), "LINE-FINAL-01", lineCd))
        Dim barcodeSn As String = BarcodeService.GenerateBarcodeSn(ProcessStep.Assembly)
        Dim workDate As String = DateTime.Now.ToString("yyyy-MM-dd")

        logger($"[바코드 스캔] LOT: {lotNo} | 바코드: {barcodeSn} | 완제품: {itemCd}")

        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using tx As SQLiteTransaction = conn.BeginTransaction()
                Try
                    ' ====== STEP 1: 설비 인터락 체크 ======
                    Dim targetLine As String = If(String.IsNullOrEmpty(lineCd), "LINE-FINAL-01", lineCd)
                    logger("   [STEP 1] 설비 인터락 사전 점검 시작...")

                    Dim interlockResults = _equipService.CheckInterlock(conn, tx, targetLine)
                    Dim failedInterlocks = EquipmentService.GetFailedInterlocks(interlockResults)
                    Dim warningInterlocks = EquipmentService.GetWarningInterlocks(interlockResults)

                    ' 경고 로그 출력
                    For Each warn In warningInterlocks
                        logger($"   [경고] {warn.Message}")
                    Next

                    ' 인터락 실패 시 차단
                    If failedInterlocks.Count > 0 Then
                        logger("   [인터락 차단] 설비 조건 미충족:")
                        For Each fail In failedInterlocks
                            logger($"     - {fail.Message}")
                        Next

                        ' LOT 추적: 차단 상태 기록
                        _lotRepo.InsertLotTrace(conn, tx, lotNo, barcodeSn, itemCd, targetLine, CInt(ProcessStep.Assembly), "")
                        tx.Commit() ' LOT 이력은 기록
                        Return False
                    End If

                    logger("   [STEP 1 통과] 설비 인터락 전 항목 정상")

                    ' ====== STEP 2: 공정 시퀀스 시뮬레이션 (LOT 추적 기록) ======
                    logger("   [STEP 2] 공정 시퀀스 흐름 실행...")
                    For Each procStep In ProcessSequence
                        Dim stepBarcode = BarcodeService.GenerateBarcodeSn(procStep)
                        Dim stepName As String = GetProcessStepName(procStep)

                        ' 각 공정 단계 LOT 추적 기록
                        _lotRepo.InsertLotTrace(conn, tx, lotNo, stepBarcode, itemCd, targetLine, CInt(procStep), "")
                        logger("     [" & stepName & "] 바코드: " & stepBarcode & " -> 공정 투입")

                        ' 공정 완료 처리
                        _lotRepo.CompleteLotProcess(conn, tx, lotNo, CInt(procStep), "OK")
                        logger("     [" & stepName & "] 공정 완료 (OK)")
                    Next

                    ' ====== STEP 3: BOM 전개 및 소요량 계산 ======
                    logger("   [STEP 3] BOM 전개 및 소요량 산출...")
                    Dim rawBomList As New List(Of BomNode)()
                    _bomRepo.GetBomRecursive(conn, tx, itemCd, 1.0, rawBomList)

                    ' 동일 부품 소요량 병합
                    Dim consolidatedBom As New Dictionary(Of String, Double)()
                    For Each node In rawBomList
                        If consolidatedBom.ContainsKey(node.ItemCd) Then
                            consolidatedBom(node.ItemCd) += node.UnitQty
                        Else
                            consolidatedBom.Add(node.ItemCd, node.UnitQty)
                        End If
                    Next

                    ' ====== STEP 4: Poka-Yoke 재고 사전 검증 ======
                    logger("   [STEP 4] Poka-Yoke 재고 사전 검증...")
                    Dim shortages As New List(Of ShortageInfo)()
                    For Each kvp In consolidatedBom
                        Dim required As Double = kvp.Value * prodQty
                        Dim current As Double = _stockRepo.GetCurrentStock(conn, tx, kvp.Key, "WH01")
                        If current < required Then
                            shortages.Add(New ShortageInfo() With {
                                .ItemCd = kvp.Key,
                                .RequiredQty = required,
                                .CurrentQty = current
                            })
                        End If
                    Next

                    If shortages.Count > 0 Then
                        logger("   [Poka-Yoke 차단] 부품 소요량 부족 -> 전체 트랜잭션 롤백:")
                        For Each s In shortages
                            logger($"     - {s.ToString()}")
                        Next
                        tx.Rollback()
                        Return False
                    End If

                    logger("   [STEP 4 통과] 전 부품 재고 충분")

                    ' ====== STEP 5: 생산 실적 기록 ======
                    logger("   [STEP 5] 생산 실적 적재...")
                    Dim logNo As Long = _prodRepo.InsertProductionLog(conn, tx, lotNo, itemCd, prodQty, workDate, targetLine, "Assembly")

                    ' ====== STEP 6: 백플러시 차감 (원자적) ======
                    logger("   [STEP 6] 백플러시 자재 차감 실행...")
                    For Each kvp In consolidatedBom
                        Dim deduct As Double = kvp.Value * prodQty
                        Dim before As Double = _stockRepo.GetCurrentStock(conn, tx, kvp.Key, "WH01")
                        Dim after As Double = before - deduct

                        _stockRepo.UpdateStock(conn, tx, kvp.Key, "WH01", after)
                        _stockRepo.InsertStockHistory(conn, tx, logNo, kvp.Key, "WH01", -deduct, before, after, "BACKFLUSH")
                    Next

                    ' ====== STEP 7: 완제품 입고 ======
                    logger("   [STEP 7] 완제품 입고 처리...")
                    Dim fgBefore As Double = _stockRepo.GetCurrentStock(conn, tx, itemCd, "WH01")
                    Dim fgAfter As Double = fgBefore + prodQty
                    _stockRepo.UpdateStock(conn, tx, itemCd, "WH01", fgAfter)
                    _stockRepo.InsertStockHistory(conn, tx, logNo, itemCd, "WH01", prodQty, fgBefore, fgAfter, "PRODUCTION")

                    ' ====== COMMIT ======
                    tx.Commit()
                    logger($"   [공정 완료] 실적 No: {logNo} | {itemCd} 조립 입고 완료 (LOT: {lotNo})")
                    Return True

                Catch ex As StockShortageException
                    tx.Rollback()
                    logger($"   [Poka-Yoke 롤백] {ex.Message}")
                    Return False
                Catch ex As InterlockException
                    tx.Rollback()
                    logger($"   [인터락 롤백] {ex.Message}")
                    Return False
                Catch ex As ErpException
                    tx.Rollback()
                    logger($"   [비즈니스 오류 롤백] [{ex.ErrorCode}] {ex.Message}")
                    Return False
                Catch ex As Exception
                    tx.Rollback()
                    logger($"   [치명적 오류 롤백] {ex.Message}")
                    Return False
                End Try
            End Using
        End Using
    End Function

    ''' <summary>공정 단계 한글명 반환</summary>
    Public Shared Function GetProcessStepName(procStep As ProcessStep) As String
        Select Case procStep
            Case ProcessStep.Press : Return "프레스"
            Case ProcessStep.Welding : Return "용접"
            Case ProcessStep.Painting : Return "도장"
            Case ProcessStep.Assembly : Return "조립"
            Case ProcessStep.Inspection : Return "검사"
            Case Else : Return "알수없음"
        End Select
    End Function
End Class
