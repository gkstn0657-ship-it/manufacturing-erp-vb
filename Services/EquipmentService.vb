Imports System.Data.SQLite
Imports System.Collections.Generic

''' <summary>
''' 설비 관리 및 인터락 서비스
''' 공정 시작 전 설비 상태 점검, 인터락 조건 체크
''' </summary>
Public Class EquipmentService

    Private ReadOnly _equipRepo As New EquipmentRepository()

    ''' <summary>
    ''' 설비 인터락 체크 (공정 시작 전 필수 검증)
    ''' 온도 범위, 사이클 한도, 설비 상태를 종합 점검
    ''' </summary>
    Public Function CheckInterlock(conn As SQLiteConnection, tx As SQLiteTransaction, lineCd As String) As List(Of InterlockCheckResult)
        Dim results As New List(Of InterlockCheckResult)()
        Dim equipments = _equipRepo.GetEquipmentByLine(conn, tx, lineCd)

        For Each equip In equipments
            Dim conditions = _equipRepo.GetInterlockConditions(conn, tx, equip.EquipCd)

            For Each cond In conditions
                Dim checkResult As New InterlockCheckResult() With {
                    .EquipCd = equip.EquipCd,
                    .ConditionType = cond.ConditionType,
                    .CheckedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }

                Select Case cond.ConditionType
                    Case "TEMP_RANGE"
                        checkResult.CurrentValue = equip.CurrentTemp
                        If equip.CurrentTemp < cond.MinValue OrElse equip.CurrentTemp > cond.MaxValue Then
                            checkResult.Result = InterlockResult.Fail
                            checkResult.Message = $"[{equip.EquipNm}] {cond.ParamName} 이탈: {equip.CurrentTemp:F1}C (허용: {cond.MinValue}~{cond.MaxValue}C)"
                        Else
                            checkResult.Result = InterlockResult.Pass
                            checkResult.Message = $"[{equip.EquipNm}] {cond.ParamName} 정상: {equip.CurrentTemp:F1}C"
                        End If

                    Case "CYCLE_LIMIT"
                        checkResult.CurrentValue = equip.CycleCount
                        If equip.CycleCount >= equip.MaxCycleBeforeMaint Then
                            checkResult.Result = InterlockResult.Fail
                            checkResult.Message = $"[{equip.EquipNm}] 보전 주기 도달: {equip.CycleCount}/{equip.MaxCycleBeforeMaint} (보전 필요)"
                        ElseIf equip.CycleCount >= equip.MaxCycleBeforeMaint * 0.9 Then
                            checkResult.Result = InterlockResult.Warning
                            checkResult.Message = $"[{equip.EquipNm}] 보전 주기 임박: {equip.CycleCount}/{equip.MaxCycleBeforeMaint} (90% 도달)"
                        Else
                            checkResult.Result = InterlockResult.Pass
                            checkResult.Message = $"[{equip.EquipNm}] 누적 작동: {equip.CycleCount}/{equip.MaxCycleBeforeMaint}"
                        End If

                    Case "STATUS_CHECK"
                        checkResult.CurrentValue = equip.Status
                        If equip.Status <> EquipmentStatus.Normal Then
                            checkResult.Result = InterlockResult.Fail
                            checkResult.Message = $"[{equip.EquipNm}] 설비 비정상 상태: {equip.Status} (에러: {If(String.IsNullOrEmpty(equip.ErrorCode), "N/A", equip.ErrorCode)})"
                        Else
                            checkResult.Result = InterlockResult.Pass
                            checkResult.Message = $"[{equip.EquipNm}] 설비 상태 정상"
                        End If

                    Case Else
                        checkResult.Result = InterlockResult.Pass
                        checkResult.Message = $"[{equip.EquipNm}] 알 수 없는 조건 타입: {cond.ConditionType}"
                End Select

                results.Add(checkResult)
            Next

            ' 인터락 통과 시 사이클 카운트 증가
            If Not results.Exists(Function(r) r.EquipCd = equip.EquipCd AndAlso r.Result = InterlockResult.Fail) Then
                _equipRepo.IncrementCycleCount(conn, tx, equip.EquipCd)
            End If
        Next

        Return results
    End Function

    ''' <summary>인터락 실패 항목만 필터링</summary>
    Public Shared Function GetFailedInterlocks(results As List(Of InterlockCheckResult)) As List(Of InterlockCheckResult)
        Return results.FindAll(Function(r) r.Result = InterlockResult.Fail)
    End Function

    ''' <summary>인터락 경고 항목만 필터링</summary>
    Public Shared Function GetWarningInterlocks(results As List(Of InterlockCheckResult)) As List(Of InterlockCheckResult)
        Return results.FindAll(Function(r) r.Result = InterlockResult.Warning)
    End Function
End Class
