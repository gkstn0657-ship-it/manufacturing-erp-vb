Imports System.Data
Imports System.Data.SQLite
Imports System.Collections.Generic

''' <summary>
''' Repository 계층: 데이터베이스 CRUD 작업을 캡슐화
''' UI와 비즈니스 로직에서 직접 SQL을 사용하지 않도록 분리
''' </summary>

' ============================================================
' 사용자 Repository
' ============================================================
Public Class UserRepository

    ''' <summary>
    ''' 사용자 인증 (로그인).
    ''' PBKDF2는 per-user salt 때문에 같은 비밀번호도 해시가 매번 달라지므로
    ''' SQL에서 해시를 동등 비교할 수 없다. 따라서 ID로 사용자를 먼저 조회한 뒤
    ''' 저장된 해시와 평문을 PasswordHasher.Verify로 검증한다.
    ''' 인증 성공 시, 구형(SHA-256) 해시는 PBKDF2로 자동 업그레이드한다.
    ''' </summary>
    Public Function Authenticate(userId As String, password As String) As UserAccount
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim storedHash As String = Nothing
            Dim user As UserAccount = Nothing

            Dim sql = "SELECT USER_ID, USER_NAME, PASSWORD_HASH, ROLE FROM USER_MASTER WHERE USER_ID = @id AND IS_ACTIVE = 1"
            Using cmd As New SQLiteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@id", userId)
                Using reader = cmd.ExecuteReader()
                    If reader.Read() Then
                        storedHash = reader("PASSWORD_HASH").ToString()
                        user = New UserAccount() With {
                            .UserId = reader("USER_ID").ToString(),
                            .UserName = reader("USER_NAME").ToString(),
                            .Role = CType(Convert.ToInt32(reader("ROLE")), UserRole),
                            .IsActive = True
                        }
                    End If
                End Using
            End Using

            ' 사용자가 없어도 동일한 검증 비용을 들여 타이밍 기반 계정 존재 추측을 방지
            If user Is Nothing Then
                PasswordHasher.Verify(password, "PBKDF2$1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")
                Return Nothing
            End If

            If Not PasswordHasher.Verify(password, storedHash) Then
                Return Nothing
            End If

            ' 구형 해시면 PBKDF2로 자동 재해싱(업그레이드)
            If PasswordHasher.NeedsRehash(storedHash) Then
                Using upgCmd As New SQLiteCommand("UPDATE USER_MASTER SET PASSWORD_HASH = @pw WHERE USER_ID = @id", conn)
                    upgCmd.Parameters.AddWithValue("@pw", PasswordHasher.Hash(password))
                    upgCmd.Parameters.AddWithValue("@id", userId)
                    upgCmd.ExecuteNonQuery()
                End Using
            End If

            ' 마지막 로그인 시각 갱신
            Using updCmd As New SQLiteCommand("UPDATE USER_MASTER SET LAST_LOGIN_AT = datetime('now','localtime') WHERE USER_ID = @id", conn)
                updCmd.Parameters.AddWithValue("@id", userId)
                updCmd.ExecuteNonQuery()
            End Using

            Return user
        End Using
    End Function

    ''' <summary>전체 품목 코드+이름 목록 (콤보박스용)</summary>
    Public Function GetAllItemList() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT ITEM_CD, ITEM_NM, ITEM_TYPE FROM ITEM_MASTER ORDER BY ITEM_TYPE, ITEM_CD"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    ''' <summary>전체 사용자 조회</summary>
    Public Function GetAllUsers() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT USER_ID AS [사용자ID], USER_NAME AS [이름], CASE ROLE WHEN 1 THEN '관리자' WHEN 2 THEN '생산관리자' ELSE '조회전용' END AS [역할], CASE IS_ACTIVE WHEN 1 THEN '활성' ELSE '비활성' END AS [상태], LAST_LOGIN_AT AS [최종로그인] FROM USER_MASTER ORDER BY ROLE ASC"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function
End Class

' ============================================================
' 재고 Repository
' ============================================================
Public Class StockRepository

    ''' <summary>품목별 현재 재고 조회</summary>
    Public Function GetCurrentStock(conn As SQLiteConnection, tx As SQLiteTransaction, itemCd As String, whCd As String) As Double
        Dim sql = "SELECT CURRENT_QTY FROM STOCK_MASTER WHERE ITEM_CD = @cd AND WH_CD = @wh"
        Dim p As New Dictionary(Of String, Object) From {{"@cd", itemCd}, {"@wh", whCd}}
        Dim result = DatabaseHelper.ExecuteScalarInTransaction(conn, tx, sql, p)
        If result IsNot Nothing AndAlso result IsNot DBNull.Value Then Return Convert.ToDouble(result)
        Return 0
    End Function

    ''' <summary>재고 수량 갱신</summary>
    Public Sub UpdateStock(conn As SQLiteConnection, tx As SQLiteTransaction, itemCd As String, whCd As String, newQty As Double)
        Dim sql = "UPDATE STOCK_MASTER SET CURRENT_QTY = @qty, UPDATED_AT = datetime('now','localtime') WHERE ITEM_CD = @cd AND WH_CD = @wh"
        Dim p As New Dictionary(Of String, Object) From {{"@qty", newQty}, {"@cd", itemCd}, {"@wh", whCd}}
        DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
    End Sub

    ''' <summary>재고 변동 이력 기록</summary>
    Public Sub InsertStockHistory(conn As SQLiteConnection, tx As SQLiteTransaction, logNo As Long, itemCd As String, whCd As String, changeQty As Double, beforeQty As Double, afterQty As Double, changeType As String)
        Dim sql = "INSERT INTO STOCK_HISTORY (LOG_NO, ITEM_CD, WH_CD, CHANGE_QTY, BEFORE_QTY, AFTER_QTY, CHANGE_TYPE) VALUES (@log,@cd,@wh,@chg,@bef,@aft,@tp)"
        Dim p As New Dictionary(Of String, Object) From {
            {"@log", logNo}, {"@cd", itemCd}, {"@wh", whCd},
            {"@chg", changeQty}, {"@bef", beforeQty}, {"@aft", afterQty}, {"@tp", changeType}
        }
        DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
    End Sub

    ''' <summary>대시보드용 전체 재고 현황</summary>
    Public Function GetDashboardStock() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT m.ITEM_CD AS [품목코드], m.ITEM_NM AS [품목명], " &
                "CASE m.ITEM_TYPE WHEN 'FG' THEN '완제품(FG)' WHEN 'SFG' THEN '반제품(SFG)' ELSE '원자재(RM)' END AS [품목타입], " &
                "s.CURRENT_QTY AS [현재재고], s.SAFETY_QTY AS [안전재고], s.UPDATED_AT AS [최종변동일시] " &
                "FROM ITEM_MASTER m JOIN STOCK_MASTER s ON m.ITEM_CD = s.ITEM_CD ORDER BY m.ITEM_TYPE ASC, m.ITEM_CD ASC"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    ''' <summary>품목별 재고 변동 이력 조회</summary>
    Public Function GetStockHistory(itemCd As String) As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT HIST_NO AS [이력No], LOG_NO AS [실적No], CHANGE_QTY AS [변동량], BEFORE_QTY AS [변동전], AFTER_QTY AS [변동후], " &
                "CASE CHANGE_TYPE WHEN 'INIT' THEN '초기값' WHEN 'PRODUCTION' THEN '완제품입고' ELSE '백플러시차감' END AS [구분], " &
                "CREATED_AT AS [처리일시] FROM STOCK_HISTORY WHERE ITEM_CD = @cd ORDER BY HIST_NO DESC"
            Using cmd As New SQLiteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@cd", itemCd)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    ''' <summary>전체 재고 변동 타임라인 (최근 100건)</summary>
    Public Function GetFullStockHistory() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT h.HIST_NO AS [이력No], h.LOG_NO AS [실적No], h.ITEM_CD AS [품목코드], i.ITEM_NM AS [품목명], " &
                "h.CHANGE_QTY AS [변동량], h.BEFORE_QTY AS [변동전], h.AFTER_QTY AS [변동후], h.CHANGE_TYPE AS [타입], " &
                "h.CREATED_AT AS [처리일시] FROM STOCK_HISTORY h JOIN ITEM_MASTER i ON h.ITEM_CD = i.ITEM_CD ORDER BY h.HIST_NO DESC LIMIT 100"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    ''' <summary>품목 코드 중복 확인</summary>
    Public Function ItemExists(itemCd As String) As Boolean
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using cmd As New SQLiteCommand("SELECT COUNT(*) FROM ITEM_MASTER WHERE ITEM_CD = @cd", conn)
                cmd.Parameters.AddWithValue("@cd", itemCd)
                Return Convert.ToInt32(cmd.ExecuteScalar()) > 0
            End Using
        End Using
    End Function

    ''' <summary>품목 + 초기 재고 등록 (트랜잭션)</summary>
    Public Sub RegisterNewItem(itemCd As String, itemNm As String, itemType As String, initQty As Double, safetyQty As Double)
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using tx = conn.BeginTransaction()
                Try
                    ' 품목 마스터 INSERT
                    Dim sqlItem = "INSERT INTO ITEM_MASTER (ITEM_CD, ITEM_NM, ITEM_TYPE) VALUES (@cd, @nm, @tp)"
                    Dim pItem As New Dictionary(Of String, Object) From {{"@cd", itemCd}, {"@nm", itemNm}, {"@tp", itemType}}
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sqlItem, pItem)

                    ' 재고 마스터 INSERT
                    Dim sqlStock = "INSERT INTO STOCK_MASTER (ITEM_CD, WH_CD, CURRENT_QTY, SAFETY_QTY) VALUES (@cd, 'WH01', @qty, @sq)"
                    Dim pStock As New Dictionary(Of String, Object) From {{"@cd", itemCd}, {"@qty", initQty}, {"@sq", safetyQty}}
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sqlStock, pStock)

                    ' 초기 재고 이력 기록
                    If initQty > 0 Then
                        Dim sqlHist = "INSERT INTO STOCK_HISTORY (LOG_NO, ITEM_CD, WH_CD, CHANGE_QTY, BEFORE_QTY, AFTER_QTY, CHANGE_TYPE) VALUES (0, @cd, 'WH01', @qty, 0, @qty, 'INIT')"
                        Dim pHist As New Dictionary(Of String, Object) From {{"@cd", itemCd}, {"@qty", initQty}}
                        DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sqlHist, pHist)
                    End If

                    tx.Commit()
                Catch
                    tx.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    ''' <summary>BOM 관계 등록 (부모-자식)</summary>
    Public Sub RegisterBomLink(parentCd As String, childCd As String, qty As Double)
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using cmd As New SQLiteCommand("INSERT OR REPLACE INTO BOM_MASTER (PARENT_CD, CHILD_CD, QTY) VALUES (@p, @c, @q)", conn)
                cmd.Parameters.AddWithValue("@p", parentCd)
                cmd.Parameters.AddWithValue("@c", childCd)
                cmd.Parameters.AddWithValue("@q", qty)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>BOM 관계 삭제</summary>
    Public Sub DeleteBomLink(parentCd As String, childCd As String)
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using cmd As New SQLiteCommand("DELETE FROM BOM_MASTER WHERE PARENT_CD = @p AND CHILD_CD = @c", conn)
                cmd.Parameters.AddWithValue("@p", parentCd)
                cmd.Parameters.AddWithValue("@c", childCd)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>품목 삭제 (품목 + 재고 + BOM 연관 전부)</summary>
    Public Sub DeleteItem(itemCd As String)
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using tx = conn.BeginTransaction()
                Try
                    Dim p As New Dictionary(Of String, Object) From {{"@cd", itemCd}}
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, "DELETE FROM BOM_MASTER WHERE PARENT_CD = @cd OR CHILD_CD = @cd", p)
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, "DELETE FROM STOCK_HISTORY WHERE ITEM_CD = @cd", p)
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, "DELETE FROM STOCK_MASTER WHERE ITEM_CD = @cd", p)
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, "DELETE FROM ITEM_MASTER WHERE ITEM_CD = @cd", p)
                    tx.Commit()
                Catch
                    tx.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    ''' <summary>재고 수동 조정 (입고/출고/조정)</summary>
    Public Sub AdjustStock(itemCd As String, adjustQty As Double, changeType As String)
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using tx = conn.BeginTransaction()
                Try
                    ' 현재 재고 조회
                    Dim beforeQty As Double = 0
                    Using cmd As New SQLiteCommand("SELECT CURRENT_QTY FROM STOCK_MASTER WHERE ITEM_CD = @cd AND WH_CD = 'WH01'", conn, tx)
                        cmd.Parameters.AddWithValue("@cd", itemCd)
                        Dim result = cmd.ExecuteScalar()
                        If result IsNot Nothing AndAlso Not DBNull.Value.Equals(result) Then
                            beforeQty = Convert.ToDouble(result)
                        End If
                    End Using

                    Dim afterQty As Double = beforeQty + adjustQty
                    If afterQty < 0 Then
                        Dim sList As New List(Of ShortageInfo)()
                        sList.Add(New ShortageInfo() With {.ItemCd = itemCd, .RequiredQty = Math.Abs(adjustQty), .CurrentQty = beforeQty})
                        Throw New StockShortageException(sList)
                    End If

                    ' 재고 업데이트
                    Dim pUpd As New Dictionary(Of String, Object) From {{"@cd", itemCd}, {"@qty", afterQty}}
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, "UPDATE STOCK_MASTER SET CURRENT_QTY = @qty WHERE ITEM_CD = @cd AND WH_CD = 'WH01'", pUpd)

                    ' 이력 기록
                    Dim sqlHist = "INSERT INTO STOCK_HISTORY (LOG_NO, ITEM_CD, WH_CD, CHANGE_QTY, BEFORE_QTY, AFTER_QTY, CHANGE_TYPE) VALUES (0, @cd, 'WH01', @chg, @bf, @af, @tp)"
                    Dim pHist As New Dictionary(Of String, Object) From {
                        {"@cd", itemCd}, {"@chg", adjustQty}, {"@bf", beforeQty}, {"@af", afterQty}, {"@tp", changeType}
                    }
                    DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sqlHist, pHist)

                    tx.Commit()
                Catch
                    tx.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    ''' <summary>특정 품목의 BOM 자식 목록 (그리드 표시용)</summary>
    Public Function GetBomChildrenTable(parentCd As String) As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT b.CHILD_CD AS [자식품목코드], i.ITEM_NM AS [품목명], " &
                "CASE i.ITEM_TYPE WHEN 'FG' THEN '완제품' WHEN 'SFG' THEN '반제품' ELSE '원자재' END AS [타입], " &
                "b.QTY AS [소요량] " &
                "FROM BOM_MASTER b JOIN ITEM_MASTER i ON b.CHILD_CD = i.ITEM_CD WHERE b.PARENT_CD = @cd ORDER BY i.ITEM_TYPE, b.CHILD_CD"
            Using cmd As New SQLiteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@cd", parentCd)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function
End Class

' ============================================================
' 생산 Repository
' ============================================================
Public Class ProductionRepository

    ''' <summary>생산 실적 기록 (INSERT 후 LOG_NO 반환)</summary>
    Public Function InsertProductionLog(conn As SQLiteConnection, tx As SQLiteTransaction, lotNo As String, itemCd As String, prodQty As Double, workDate As String, lineCd As String, processStep As String) As Long
        Dim sql = "INSERT INTO PRODUCTION_LOG (LOT_NO, ITEM_CD, PROD_QTY, WORK_DATE, STATUS, LINE_CD, PROCESS_STEP) VALUES (@lot,@item,@qty,@dt,'OK',@ln,@ps); SELECT last_insert_rowid();"
        Dim p As New Dictionary(Of String, Object) From {
            {"@lot", lotNo}, {"@item", itemCd}, {"@qty", prodQty},
            {"@dt", workDate}, {"@ln", lineCd}, {"@ps", processStep}
        }
        Return Convert.ToInt64(DatabaseHelper.ExecuteScalarInTransaction(conn, tx, sql, p))
    End Function

    ''' <summary>생산 실적 KPI (총 건수, 총 수량)</summary>
    Public Function GetProductionKpi() As Tuple(Of Integer, Double)
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Using cmd As New SQLiteCommand("SELECT COUNT(*) AS logs, COALESCE(SUM(PROD_QTY),0) AS qty FROM PRODUCTION_LOG", conn)
                Using reader = cmd.ExecuteReader()
                    If reader.Read() Then
                        Return Tuple.Create(Convert.ToInt32(reader("logs")), Convert.ToDouble(reader("qty")))
                    End If
                End Using
            End Using
        End Using
        Return Tuple.Create(0, 0.0)
    End Function

    ''' <summary>생산 실적 백로그 (최근 50건)</summary>
    Public Function GetProductionLog() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT LOG_NO AS [실적No], LOT_NO AS [생산LOT], ITEM_CD AS [완제품코드], PROD_QTY AS [생산수량], " &
                "COALESCE(LINE_CD, '-') AS [라인], COALESCE(PROCESS_STEP, '-') AS [공정], WORK_DATE AS [생산일자] " &
                "FROM PRODUCTION_LOG ORDER BY LOG_NO DESC LIMIT 50"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function
End Class

' ============================================================
' BOM Repository
' ============================================================
Public Class BomRepository

    ''' <summary>BOM 재귀 전개 (소요량 계산)</summary>
    Public Sub GetBomRecursive(conn As SQLiteConnection, tx As SQLiteTransaction, parentCd As String, parentQty As Double, ByRef tree As List(Of BomNode))
        Dim sql = "SELECT b.CHILD_CD, b.QTY, i.ITEM_TYPE FROM BOM_MASTER b JOIN ITEM_MASTER i ON b.CHILD_CD = i.ITEM_CD WHERE b.PARENT_CD = @parent"
        Using cmd As New SQLiteCommand(sql, conn, tx)
            cmd.Parameters.AddWithValue("@parent", parentCd)
            Using reader = cmd.ExecuteReader()
                Dim local As New List(Of Tuple(Of String, Double, String))()
                While reader.Read()
                    local.Add(Tuple.Create(reader("CHILD_CD").ToString(), Convert.ToDouble(reader("QTY")), reader("ITEM_TYPE").ToString()))
                End While
                For Each itm In local
                    Dim totalUnitQty As Double = parentQty * itm.Item2
                    tree.Add(New BomNode() With {.ItemCd = itm.Item1, .UnitQty = totalUnitQty, .ItemType = itm.Item3})
                    If itm.Item3 = "SFG" Then
                        GetBomRecursive(conn, tx, itm.Item1, totalUnitQty, tree)
                    End If
                Next
            End Using
        End Using
    End Sub

    ''' <summary>BOM 트리뷰 구성용 조회 (UI용)</summary>
    Public Function GetBomChildren(conn As SQLiteConnection, parentCd As String) As List(Of Tuple(Of String, Double, String, String))
        Dim result As New List(Of Tuple(Of String, Double, String, String))()
        Dim sql = "SELECT b.CHILD_CD, b.QTY, i.ITEM_NM, i.ITEM_TYPE FROM BOM_MASTER b JOIN ITEM_MASTER i ON b.CHILD_CD = i.ITEM_CD WHERE b.PARENT_CD = @parent"
        Using cmd As New SQLiteCommand(sql, conn)
            cmd.Parameters.AddWithValue("@parent", parentCd)
            Using reader = cmd.ExecuteReader()
                While reader.Read()
                    result.Add(Tuple.Create(reader("CHILD_CD").ToString(), Convert.ToDouble(reader("QTY")), reader("ITEM_NM").ToString(), reader("ITEM_TYPE").ToString()))
                End While
            End Using
        End Using
        Return result
    End Function
End Class

' ============================================================
' 설비 Repository
' ============================================================
Public Class EquipmentRepository

    ''' <summary>설비 현황 전체 조회</summary>
    Public Function GetEquipmentDashboard() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT e.EQUIP_CD AS [설비코드], e.EQUIP_NM AS [설비명], l.LINE_NM AS [라인], " &
                "CASE e.PROCESS_STEP WHEN 10 THEN '프레스' WHEN 20 THEN '용접' WHEN 30 THEN '도장' WHEN 40 THEN '조립' WHEN 50 THEN '검사' END AS [공정], " &
                "CASE e.STATUS WHEN 0 THEN '정상' WHEN 1 THEN '경고' WHEN 2 THEN '고장' WHEN 3 THEN '보전중' ELSE '유휴' END AS [상태], " &
                "CASE WHEN e.CURRENT_TEMP > 0 THEN ROUND(e.CURRENT_TEMP, 1) || ' C' ELSE '-' END AS [현재온도], " &
                "e.CYCLE_COUNT AS [누적작동], COALESCE(e.ERROR_CODE, '-') AS [에러코드] " &
                "FROM EQUIPMENT_MASTER e LEFT JOIN LINE_MASTER l ON e.LINE_CD = l.LINE_CD ORDER BY e.PROCESS_STEP, e.EQUIP_CD"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    ''' <summary>특정 라인의 설비 목록 조회</summary>
    Public Function GetEquipmentByLine(conn As SQLiteConnection, tx As SQLiteTransaction, lineCd As String) As List(Of EquipmentMaster)
        Dim result As New List(Of EquipmentMaster)()
        Dim sql = "SELECT * FROM EQUIPMENT_MASTER WHERE LINE_CD = @ln"
        Using cmd As New SQLiteCommand(sql, conn, tx)
            cmd.Parameters.AddWithValue("@ln", lineCd)
            Using reader = cmd.ExecuteReader()
                While reader.Read()
                    result.Add(New EquipmentMaster() With {
                        .EquipCd = reader("EQUIP_CD").ToString(),
                        .EquipNm = reader("EQUIP_NM").ToString(),
                        .LineCd = reader("LINE_CD").ToString(),
                        .ProcessStep = CType(Convert.ToInt32(reader("PROCESS_STEP")), ProcessStep),
                        .Status = CType(Convert.ToInt32(reader("STATUS")), EquipmentStatus),
                        .CycleCount = Convert.ToInt64(reader("CYCLE_COUNT")),
                        .MaxCycleBeforeMaint = Convert.ToInt64(reader("MAX_CYCLE_BEFORE_MAINT")),
                        .CurrentTemp = Convert.ToDouble(reader("CURRENT_TEMP")),
                        .TempUpperLimit = Convert.ToDouble(reader("TEMP_UPPER_LIMIT")),
                        .ErrorCode = If(reader("ERROR_CODE") Is DBNull.Value, "", reader("ERROR_CODE").ToString())
                    })
                End While
            End Using
        End Using
        Return result
    End Function

    ''' <summary>설비 사이클 카운트 증가</summary>
    Public Sub IncrementCycleCount(conn As SQLiteConnection, tx As SQLiteTransaction, equipCd As String)
        Dim sql = "UPDATE EQUIPMENT_MASTER SET CYCLE_COUNT = CYCLE_COUNT + 1 WHERE EQUIP_CD = @cd"
        Dim p As New Dictionary(Of String, Object) From {{"@cd", equipCd}}
        DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
    End Sub

    ''' <summary>인터락 조건 조회</summary>
    Public Function GetInterlockConditions(conn As SQLiteConnection, tx As SQLiteTransaction, equipCd As String) As List(Of InterlockCondition)
        Dim result As New List(Of InterlockCondition)()
        Dim sql = "SELECT * FROM INTERLOCK_CONDITION WHERE EQUIP_CD = @cd AND IS_ACTIVE = 1"
        Using cmd As New SQLiteCommand(sql, conn, tx)
            cmd.Parameters.AddWithValue("@cd", equipCd)
            Using reader = cmd.ExecuteReader()
                While reader.Read()
                    result.Add(New InterlockCondition() With {
                        .InterlockId = Convert.ToInt32(reader("INTERLOCK_ID")),
                        .EquipCd = reader("EQUIP_CD").ToString(),
                        .ConditionType = reader("CONDITION_TYPE").ToString(),
                        .ParamName = If(reader("PARAM_NAME") Is DBNull.Value, "", reader("PARAM_NAME").ToString()),
                        .MinValue = Convert.ToDouble(reader("MIN_VALUE")),
                        .MaxValue = Convert.ToDouble(reader("MAX_VALUE")),
                        .Description = If(reader("DESCRIPTION") Is DBNull.Value, "", reader("DESCRIPTION").ToString())
                    })
                End While
            End Using
        End Using
        Return result
    End Function

    ''' <summary>라인 마스터 전체 조회</summary>
    Public Function GetLineDashboard() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT LINE_CD AS [라인코드], LINE_NM AS [라인명], " &
                "CASE PROCESS_STEP WHEN 10 THEN '프레스' WHEN 20 THEN '용접' WHEN 30 THEN '도장' WHEN 40 THEN '조립' WHEN 50 THEN '검사' END AS [공정], " &
                "TAKT_TIME || '초' AS [택트타임], CAPACITY || '대/시간' AS [생산능력], " &
                "CASE IS_ACTIVE WHEN 1 THEN '가동' ELSE '비가동' END AS [상태] FROM LINE_MASTER ORDER BY PROCESS_STEP, LINE_CD"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function
End Class

' ============================================================
' LOT 추적 Repository
' ============================================================
Public Class LotTraceRepository

    ''' <summary>LOT 공정 이력 기록</summary>
    Public Sub InsertLotTrace(conn As SQLiteConnection, tx As SQLiteTransaction, lotNo As String, barcodeSn As String, itemCd As String, lineCd As String, processStep As Integer, equipCd As String)
        Dim sql = "INSERT OR REPLACE INTO LOT_TRACE (LOT_NO, BARCODE_SN, ITEM_CD, LINE_CD, PROCESS_STEP, PROCESS_STATUS, START_TIME, EQUIP_CD) " &
            "VALUES (@lot, @bc, @item, @ln, @ps, @status, datetime('now','localtime'), @eq)"
        Dim p As New Dictionary(Of String, Object) From {
            {"@lot", lotNo}, {"@bc", barcodeSn}, {"@item", itemCd},
            {"@ln", lineCd}, {"@ps", processStep}, {"@status", CInt(ProcessStatus.Running)}, {"@eq", equipCd}
        }
        DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
    End Sub

    ''' <summary>LOT 공정 완료 처리</summary>
    Public Sub CompleteLotProcess(conn As SQLiteConnection, tx As SQLiteTransaction, lotNo As String, processStep As Integer, qualityResult As String)
        Dim sql = "UPDATE LOT_TRACE SET PROCESS_STATUS = @status, END_TIME = datetime('now','localtime'), QUALITY_RESULT = @qr WHERE LOT_NO = @lot AND PROCESS_STEP = @ps"
        Dim p As New Dictionary(Of String, Object) From {
            {"@status", CInt(ProcessStatus.Completed)}, {"@qr", qualityResult},
            {"@lot", lotNo}, {"@ps", processStep}
        }
        DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
    End Sub

    ''' <summary>LOT별 공정 이력 조회 (Traceability)</summary>
    Public Function GetLotTraceHistory(lotNo As String) As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT t.LOT_NO AS [LOT번호], t.BARCODE_SN AS [바코드], t.ITEM_CD AS [품목코드], " &
                "CASE t.PROCESS_STEP WHEN 10 THEN '프레스' WHEN 20 THEN '용접' WHEN 30 THEN '도장' WHEN 40 THEN '조립' WHEN 50 THEN '검사' END AS [공정], " &
                "CASE t.PROCESS_STATUS WHEN 0 THEN '대기' WHEN 1 THEN '진행중' WHEN 2 THEN '완료' WHEN 3 THEN '차단' ELSE '이상' END AS [상태], " &
                "t.START_TIME AS [시작시각], t.END_TIME AS [종료시각], COALESCE(t.EQUIP_CD, '-') AS [설비], " &
                "COALESCE(t.QUALITY_RESULT, '-') AS [품질결과], COALESCE(t.DEFECT_CODE, '-') AS [불량코드] " &
                "FROM LOT_TRACE t WHERE t.LOT_NO = @lot ORDER BY t.PROCESS_STEP"
            Using cmd As New SQLiteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@lot", lotNo)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    ''' <summary>최근 LOT 추적 현황 (공정 모니터링용)</summary>
    Public Function GetRecentLotTrace() As DataTable
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()
            Dim sql = "SELECT t.LOT_NO AS [LOT번호], t.BARCODE_SN AS [바코드SN], t.ITEM_CD AS [품목코드], " &
                "COALESCE(t.LINE_CD, '-') AS [라인], " &
                "CASE t.PROCESS_STEP WHEN 10 THEN '프레스' WHEN 20 THEN '용접' WHEN 30 THEN '도장' WHEN 40 THEN '조립' WHEN 50 THEN '검사' END AS [공정단계], " &
                "CASE t.PROCESS_STATUS WHEN 0 THEN '대기' WHEN 1 THEN '진행중' WHEN 2 THEN '완료' WHEN 3 THEN '차단' ELSE '이상' END AS [상태], " &
                "t.START_TIME AS [시작], COALESCE(t.QUALITY_RESULT, '-') AS [품질] " &
                "FROM LOT_TRACE t ORDER BY t.START_TIME DESC LIMIT 100"
            Using cmd As New SQLiteCommand(sql, conn)
                Using adapter As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    adapter.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function
End Class
