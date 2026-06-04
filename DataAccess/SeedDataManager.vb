Imports System.Data.SQLite
Imports System.Collections.Generic

''' <summary>
''' 초기 마스터 데이터 시드 관리자
''' 품목, BOM, 재고, 라인, 설비, 사용자, 인터락 조건 초기 데이터 투입
''' </summary>
Public NotInheritable Class SeedDataManager

    Private Sub New()
    End Sub

    ''' <summary>전체 시드 데이터 투입 (최초 1회)</summary>
    Public Shared Sub SeedAll()
        Using conn As SQLiteConnection = DatabaseHelper.CreateConnection()
            conn.Open()

            ' 이미 데이터가 있으면 스킵
            Using checkCmd As New SQLiteCommand("SELECT COUNT(*) FROM ITEM_MASTER", conn)
                If Convert.ToInt32(checkCmd.ExecuteScalar()) > 0 Then Return
            End Using

            Using tx As SQLiteTransaction = conn.BeginTransaction()
                Try
                    SeedUsers(conn, tx)
                    SeedItems(conn, tx)
                    SeedBom(conn, tx)
                    SeedStock(conn, tx)
                    SeedLines(conn, tx)
                    SeedEquipment(conn, tx)
                    SeedInterlockConditions(conn, tx)
                    tx.Commit()
                Catch
                    tx.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    Private Shared Sub SeedUsers(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim users As New List(Of Tuple(Of String, String, String, Integer)) From {
            Tuple.Create("admin", "시스템 관리자", PasswordHasher.Hash("admin123"), 1),
            Tuple.Create("prod_mgr", "생산관리자 김철수", PasswordHasher.Hash("prod123"), 2),
            Tuple.Create("viewer", "조회전용 이영희", PasswordHasher.Hash("view123"), 3)
        }
        For Each u In users
            Dim sql = "INSERT INTO USER_MASTER (USER_ID, USER_NAME, PASSWORD_HASH, ROLE) VALUES (@id,@nm,@pw,@role)"
            Dim p As New Dictionary(Of String, Object) From {{"@id", u.Item1}, {"@nm", u.Item2}, {"@pw", u.Item3}, {"@role", u.Item4}}
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

    Private Shared Sub SeedItems(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim items As New List(Of Tuple(Of String, String, String)) From {
            Tuple.Create("FG-BODY-001", "SP5 차체 ASSY (기아 스포티지)", "FG"),
            Tuple.Create("FG-BODY-002", "NQ5 차체 ASSY (기아 스포티지 HEV)", "FG"),
            Tuple.Create("FG-CAB-001", "PU CAB ASSY (봉고 캡)", "FG"),
            Tuple.Create("SFG-DOOR-FL", "프론트 좌측 도어 패널", "SFG"),
            Tuple.Create("SFG-DOOR-FR", "프론트 우측 도어 패널", "SFG"),
            Tuple.Create("SFG-DOOR-RL", "리어 좌측 도어 패널", "SFG"),
            Tuple.Create("SFG-DOOR-RR", "리어 우측 도어 패널", "SFG"),
            Tuple.Create("SFG-HOOD-01", "후드 패널 ASSY", "SFG"),
            Tuple.Create("SFG-TRUNK-01", "트렁크 리드 ASSY", "SFG"),
            Tuple.Create("SFG-FENDER-L", "좌측 펜더 패널", "SFG"),
            Tuple.Create("SFG-FENDER-R", "우측 펜더 패널", "SFG"),
            Tuple.Create("SFG-ROOF-01", "루프 패널", "SFG"),
            Tuple.Create("SFG-SIDE-L", "좌측 사이드 패널", "SFG"),
            Tuple.Create("SFG-SIDE-R", "우측 사이드 패널", "SFG"),
            Tuple.Create("SFG-FLOOR-01", "플로어 패널 ASSY", "SFG"),
            Tuple.Create("SFG-CAB-FR", "캡 프론트 패널", "SFG"),
            Tuple.Create("SFG-CAB-SD", "캡 사이드 패널", "SFG"),
            Tuple.Create("RM-STEEL-CR", "냉연강판 (CR Steel) 0.8t", "RM"),
            Tuple.Create("RM-STEEL-HR", "열연강판 (HR Steel) 1.2t", "RM"),
            Tuple.Create("RM-STEEL-GA", "아연도금강판 (GA Steel) 1.0t", "RM"),
            Tuple.Create("RM-BOLT-M6", "볼트 M6x20", "RM"),
            Tuple.Create("RM-BOLT-M8", "볼트 M8x25", "RM"),
            Tuple.Create("RM-NUT-M6", "너트 M6", "RM"),
            Tuple.Create("RM-NUT-M8", "너트 M8", "RM"),
            Tuple.Create("RM-WELD-WIRE", "용접 와이어 1.2mm", "RM"),
            Tuple.Create("RM-SEALANT", "실란트 (구조용 접착제)", "RM"),
            Tuple.Create("RM-PRIMER", "프라이머 도료", "RM")
        }
        For Each itm In items
            Dim sql = "INSERT INTO ITEM_MASTER (ITEM_CD, ITEM_NM, ITEM_TYPE) VALUES (@cd,@nm,@tp)"
            Dim p As New Dictionary(Of String, Object) From {{"@cd", itm.Item1}, {"@nm", itm.Item2}, {"@tp", itm.Item3}}
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

    Private Shared Sub SeedBom(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim boms As New List(Of Tuple(Of String, String, Double)) From {
            Tuple.Create("FG-BODY-001", "SFG-DOOR-FL", 1.0), Tuple.Create("FG-BODY-001", "SFG-DOOR-FR", 1.0),
            Tuple.Create("FG-BODY-001", "SFG-DOOR-RL", 1.0), Tuple.Create("FG-BODY-001", "SFG-DOOR-RR", 1.0),
            Tuple.Create("FG-BODY-001", "SFG-HOOD-01", 1.0), Tuple.Create("FG-BODY-001", "SFG-TRUNK-01", 1.0),
            Tuple.Create("FG-BODY-001", "SFG-FENDER-L", 1.0), Tuple.Create("FG-BODY-001", "SFG-FENDER-R", 1.0),
            Tuple.Create("FG-BODY-001", "SFG-ROOF-01", 1.0), Tuple.Create("FG-BODY-001", "SFG-SIDE-L", 1.0),
            Tuple.Create("FG-BODY-001", "SFG-SIDE-R", 1.0), Tuple.Create("FG-BODY-001", "SFG-FLOOR-01", 1.0),
            Tuple.Create("SFG-DOOR-FL", "RM-STEEL-CR", 2.5), Tuple.Create("SFG-DOOR-FL", "RM-BOLT-M6", 8.0),
            Tuple.Create("SFG-DOOR-FL", "RM-NUT-M6", 8.0), Tuple.Create("SFG-DOOR-FL", "RM-WELD-WIRE", 0.3),
            Tuple.Create("SFG-DOOR-FL", "RM-SEALANT", 0.15),
            Tuple.Create("SFG-DOOR-FR", "RM-STEEL-CR", 2.5), Tuple.Create("SFG-DOOR-FR", "RM-BOLT-M6", 8.0),
            Tuple.Create("SFG-DOOR-FR", "RM-NUT-M6", 8.0), Tuple.Create("SFG-DOOR-FR", "RM-WELD-WIRE", 0.3),
            Tuple.Create("SFG-DOOR-FR", "RM-SEALANT", 0.15),
            Tuple.Create("SFG-DOOR-RL", "RM-STEEL-CR", 2.0), Tuple.Create("SFG-DOOR-RL", "RM-BOLT-M6", 6.0),
            Tuple.Create("SFG-DOOR-RL", "RM-NUT-M6", 6.0), Tuple.Create("SFG-DOOR-RL", "RM-WELD-WIRE", 0.25),
            Tuple.Create("SFG-DOOR-RR", "RM-STEEL-CR", 2.0), Tuple.Create("SFG-DOOR-RR", "RM-BOLT-M6", 6.0),
            Tuple.Create("SFG-DOOR-RR", "RM-NUT-M6", 6.0), Tuple.Create("SFG-DOOR-RR", "RM-WELD-WIRE", 0.25),
            Tuple.Create("SFG-HOOD-01", "RM-STEEL-GA", 4.0), Tuple.Create("SFG-HOOD-01", "RM-BOLT-M8", 6.0),
            Tuple.Create("SFG-HOOD-01", "RM-NUT-M8", 6.0), Tuple.Create("SFG-HOOD-01", "RM-WELD-WIRE", 0.5),
            Tuple.Create("SFG-HOOD-01", "RM-PRIMER", 0.2),
            Tuple.Create("SFG-TRUNK-01", "RM-STEEL-GA", 3.5), Tuple.Create("SFG-TRUNK-01", "RM-BOLT-M8", 4.0),
            Tuple.Create("SFG-TRUNK-01", "RM-NUT-M8", 4.0), Tuple.Create("SFG-TRUNK-01", "RM-WELD-WIRE", 0.4),
            Tuple.Create("SFG-FENDER-L", "RM-STEEL-CR", 1.8), Tuple.Create("SFG-FENDER-L", "RM-BOLT-M6", 4.0),
            Tuple.Create("SFG-FENDER-L", "RM-WELD-WIRE", 0.2),
            Tuple.Create("SFG-FENDER-R", "RM-STEEL-CR", 1.8), Tuple.Create("SFG-FENDER-R", "RM-BOLT-M6", 4.0),
            Tuple.Create("SFG-FENDER-R", "RM-WELD-WIRE", 0.2),
            Tuple.Create("SFG-ROOF-01", "RM-STEEL-GA", 5.0), Tuple.Create("SFG-ROOF-01", "RM-WELD-WIRE", 0.8),
            Tuple.Create("SFG-ROOF-01", "RM-SEALANT", 0.3),
            Tuple.Create("SFG-SIDE-L", "RM-STEEL-HR", 6.0), Tuple.Create("SFG-SIDE-L", "RM-BOLT-M8", 10.0),
            Tuple.Create("SFG-SIDE-L", "RM-NUT-M8", 10.0), Tuple.Create("SFG-SIDE-L", "RM-WELD-WIRE", 1.0),
            Tuple.Create("SFG-SIDE-R", "RM-STEEL-HR", 6.0), Tuple.Create("SFG-SIDE-R", "RM-BOLT-M8", 10.0),
            Tuple.Create("SFG-SIDE-R", "RM-NUT-M8", 10.0), Tuple.Create("SFG-SIDE-R", "RM-WELD-WIRE", 1.0),
            Tuple.Create("SFG-FLOOR-01", "RM-STEEL-HR", 8.0), Tuple.Create("SFG-FLOOR-01", "RM-BOLT-M8", 12.0),
            Tuple.Create("SFG-FLOOR-01", "RM-NUT-M8", 12.0), Tuple.Create("SFG-FLOOR-01", "RM-WELD-WIRE", 1.5),
            Tuple.Create("SFG-FLOOR-01", "RM-SEALANT", 0.5),
            Tuple.Create("FG-BODY-002", "SFG-DOOR-FL", 1.0), Tuple.Create("FG-BODY-002", "SFG-DOOR-FR", 1.0),
            Tuple.Create("FG-BODY-002", "SFG-DOOR-RL", 1.0), Tuple.Create("FG-BODY-002", "SFG-DOOR-RR", 1.0),
            Tuple.Create("FG-BODY-002", "SFG-HOOD-01", 1.0), Tuple.Create("FG-BODY-002", "SFG-TRUNK-01", 1.0),
            Tuple.Create("FG-BODY-002", "SFG-FENDER-L", 1.0), Tuple.Create("FG-BODY-002", "SFG-FENDER-R", 1.0),
            Tuple.Create("FG-BODY-002", "SFG-ROOF-01", 1.0), Tuple.Create("FG-BODY-002", "SFG-SIDE-L", 1.0),
            Tuple.Create("FG-BODY-002", "SFG-SIDE-R", 1.0), Tuple.Create("FG-BODY-002", "SFG-FLOOR-01", 1.0),
            Tuple.Create("FG-CAB-001", "SFG-CAB-FR", 1.0), Tuple.Create("FG-CAB-001", "SFG-CAB-SD", 2.0),
            Tuple.Create("FG-CAB-001", "SFG-ROOF-01", 1.0), Tuple.Create("FG-CAB-001", "SFG-FLOOR-01", 1.0),
            Tuple.Create("SFG-CAB-FR", "RM-STEEL-HR", 4.0), Tuple.Create("SFG-CAB-FR", "RM-BOLT-M8", 8.0),
            Tuple.Create("SFG-CAB-FR", "RM-NUT-M8", 8.0), Tuple.Create("SFG-CAB-FR", "RM-WELD-WIRE", 0.6),
            Tuple.Create("SFG-CAB-SD", "RM-STEEL-HR", 3.5), Tuple.Create("SFG-CAB-SD", "RM-BOLT-M6", 6.0),
            Tuple.Create("SFG-CAB-SD", "RM-NUT-M6", 6.0), Tuple.Create("SFG-CAB-SD", "RM-WELD-WIRE", 0.5)
        }
        For Each bom In boms
            Dim sql = "INSERT INTO BOM_MASTER (PARENT_CD, CHILD_CD, QTY) VALUES (@p,@c,@q)"
            Dim p As New Dictionary(Of String, Object) From {{"@p", bom.Item1}, {"@c", bom.Item2}, {"@q", bom.Item3}}
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

    Private Shared Sub SeedStock(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim stocks As New List(Of Tuple(Of String, Double, Double)) From {
            Tuple.Create("RM-STEEL-CR", 42.0, 15.0), Tuple.Create("RM-STEEL-HR", 120.0, 30.0),
            Tuple.Create("RM-STEEL-GA", 80.0, 20.0), Tuple.Create("RM-BOLT-M6", 400.0, 50.0),
            Tuple.Create("RM-BOLT-M8", 400.0, 50.0), Tuple.Create("RM-NUT-M6", 400.0, 50.0),
            Tuple.Create("RM-NUT-M8", 400.0, 50.0), Tuple.Create("RM-WELD-WIRE", 90.0, 10.0),
            Tuple.Create("RM-SEALANT", 40.0, 5.0), Tuple.Create("RM-PRIMER", 30.0, 3.0),
            Tuple.Create("SFG-DOOR-FL", 12.0, 3.0), Tuple.Create("SFG-DOOR-FR", 12.0, 3.0),
            Tuple.Create("SFG-DOOR-RL", 12.0, 3.0), Tuple.Create("SFG-DOOR-RR", 12.0, 3.0),
            Tuple.Create("SFG-HOOD-01", 10.0, 2.0), Tuple.Create("SFG-TRUNK-01", 10.0, 2.0),
            Tuple.Create("SFG-FENDER-L", 10.0, 2.0), Tuple.Create("SFG-FENDER-R", 10.0, 2.0),
            Tuple.Create("SFG-ROOF-01", 10.0, 2.0), Tuple.Create("SFG-SIDE-L", 10.0, 2.0),
            Tuple.Create("SFG-SIDE-R", 10.0, 2.0), Tuple.Create("SFG-FLOOR-01", 10.0, 2.0),
            Tuple.Create("SFG-CAB-FR", 10.0, 2.0), Tuple.Create("SFG-CAB-SD", 15.0, 3.0),
            Tuple.Create("FG-BODY-001", 0.0, 0.0), Tuple.Create("FG-BODY-002", 0.0, 0.0),
            Tuple.Create("FG-CAB-001", 0.0, 0.0)
        }
        For Each stk In stocks
            Dim sql = "INSERT INTO STOCK_MASTER (ITEM_CD, CURRENT_QTY, SAFETY_QTY) VALUES (@cd,@cq,@sq)"
            Dim p As New Dictionary(Of String, Object) From {{"@cd", stk.Item1}, {"@cq", stk.Item2}, {"@sq", stk.Item3}}
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

    Private Shared Sub SeedLines(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim lines As New List(Of Tuple(Of String, String, Integer, Integer, Integer)) From {
            Tuple.Create("LINE-PRESS-01", "1차 프레스 라인 (대형 패널)", 10, 45, 40),
            Tuple.Create("LINE-PRESS-02", "2차 프레스 라인 (소형 부품)", 10, 30, 60),
            Tuple.Create("LINE-WELD-01", "차체 용접 라인 (메인 바디)", 20, 60, 30),
            Tuple.Create("LINE-WELD-02", "서브 용접 라인 (도어/후드)", 20, 40, 45),
            Tuple.Create("LINE-PAINT-01", "전착/중도 도장 라인", 30, 90, 20),
            Tuple.Create("LINE-PAINT-02", "상도/클리어 도장 라인", 30, 90, 20),
            Tuple.Create("LINE-TRIM-01", "트림 조립 라인", 40, 55, 32),
            Tuple.Create("LINE-CHASSIS-01", "섀시 조립 라인", 40, 55, 32),
            Tuple.Create("LINE-FINAL-01", "파이널 조립 라인", 40, 60, 30),
            Tuple.Create("LINE-INSP-01", "품질 검사 라인", 50, 120, 15)
        }
        For Each ln In lines
            Dim sql = "INSERT INTO LINE_MASTER (LINE_CD, LINE_NM, PROCESS_STEP, TAKT_TIME, CAPACITY) VALUES (@cd,@nm,@ps,@tt,@cap)"
            Dim p As New Dictionary(Of String, Object) From {{"@cd", ln.Item1}, {"@nm", ln.Item2}, {"@ps", ln.Item3}, {"@tt", ln.Item4}, {"@cap", ln.Item5}}
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

    Private Shared Sub SeedEquipment(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim equips As New List(Of Tuple(Of String, String, String, Integer, Double, Double)) From {
            Tuple.Create("EQ-PRESS-2000T", "2000톤 프레스기 (사이드 패널)", "LINE-PRESS-01", 10, 0.0, 0.0),
            Tuple.Create("EQ-PRESS-1000T", "1000톤 프레스기 (도어 패널)", "LINE-PRESS-01", 10, 0.0, 0.0),
            Tuple.Create("EQ-PRESS-500T", "500톤 프레스기 (소형 부품)", "LINE-PRESS-02", 10, 0.0, 0.0),
            Tuple.Create("EQ-ROBOT-SP01", "스팟 용접 로봇 #1 (플로어)", "LINE-WELD-01", 20, 350.0, 500.0),
            Tuple.Create("EQ-ROBOT-SP02", "스팟 용접 로봇 #2 (사이드)", "LINE-WELD-01", 20, 350.0, 500.0),
            Tuple.Create("EQ-ROBOT-ARC01", "아크 용접 로봇 (서브 프레임)", "LINE-WELD-02", 20, 400.0, 600.0),
            Tuple.Create("EQ-ED-TANK", "전착 도장 탱크", "LINE-PAINT-01", 30, 180.0, 200.0),
            Tuple.Create("EQ-SPRAY-01", "도장 스프레이 로봇 #1", "LINE-PAINT-02", 30, 20.0, 30.0),
            Tuple.Create("EQ-OVEN-01", "건조로 (도장 경화)", "LINE-PAINT-02", 30, 140.0, 180.0),
            Tuple.Create("EQ-TORQUE-01", "전동 토크 렌치 (트림)", "LINE-TRIM-01", 40, 0.0, 0.0),
            Tuple.Create("EQ-LIFT-01", "차체 리프트 (섀시 합체)", "LINE-CHASSIS-01", 40, 0.0, 0.0),
            Tuple.Create("EQ-ALIGN-01", "휠 얼라인먼트 장비", "LINE-FINAL-01", 40, 0.0, 0.0),
            Tuple.Create("EQ-ROLLER-01", "롤러 테스트 벤치", "LINE-INSP-01", 50, 0.0, 0.0),
            Tuple.Create("EQ-LEAK-01", "수밀 검사 장비 (Rain Test)", "LINE-INSP-01", 50, 0.0, 0.0)
        }
        For Each eq In equips
            Dim sql = "INSERT INTO EQUIPMENT_MASTER (EQUIP_CD, EQUIP_NM, LINE_CD, PROCESS_STEP, CURRENT_TEMP, TEMP_UPPER_LIMIT) VALUES (@cd,@nm,@ln,@ps,@ct,@ul)"
            Dim p As New Dictionary(Of String, Object) From {
                {"@cd", eq.Item1}, {"@nm", eq.Item2}, {"@ln", eq.Item3},
                {"@ps", eq.Item4}, {"@ct", eq.Item5}, {"@ul", eq.Item6}
            }
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

    Private Shared Sub SeedInterlockConditions(conn As SQLiteConnection, tx As SQLiteTransaction)
        Dim conditions As New List(Of Tuple(Of String, String, String, Double, Double, String)) From {
            Tuple.Create("EQ-ROBOT-SP01", "TEMP_RANGE", "용접 팁 온도", 300.0, 500.0, "스팟 용접 팁 온도 범위 체크"),
            Tuple.Create("EQ-ROBOT-SP02", "TEMP_RANGE", "용접 팁 온도", 300.0, 500.0, "스팟 용접 팁 온도 범위 체크"),
            Tuple.Create("EQ-ROBOT-ARC01", "TEMP_RANGE", "아크 온도", 350.0, 600.0, "아크 용접 온도 범위 체크"),
            Tuple.Create("EQ-ED-TANK", "TEMP_RANGE", "전착액 온도", 160.0, 200.0, "전착 도장 탱크 온도 범위 체크"),
            Tuple.Create("EQ-OVEN-01", "TEMP_RANGE", "건조로 온도", 120.0, 180.0, "도장 건조로 온도 범위 체크"),
            Tuple.Create("EQ-PRESS-2000T", "CYCLE_LIMIT", "누적 타수", 0.0, 10000.0, "프레스 보전 주기 도달 체크"),
            Tuple.Create("EQ-PRESS-1000T", "CYCLE_LIMIT", "누적 타수", 0.0, 10000.0, "프레스 보전 주기 도달 체크"),
            Tuple.Create("EQ-ROBOT-SP01", "STATUS_CHECK", "설비 상태", 0.0, 0.0, "설비 정상 상태 확인"),
            Tuple.Create("EQ-ROBOT-SP02", "STATUS_CHECK", "설비 상태", 0.0, 0.0, "설비 정상 상태 확인"),
            Tuple.Create("EQ-ED-TANK", "STATUS_CHECK", "설비 상태", 0.0, 0.0, "설비 정상 상태 확인")
        }
        For Each c In conditions
            Dim sql = "INSERT INTO INTERLOCK_CONDITION (EQUIP_CD, CONDITION_TYPE, PARAM_NAME, MIN_VALUE, MAX_VALUE, DESCRIPTION) VALUES (@eq,@ct,@pn,@mn,@mx,@desc)"
            Dim p As New Dictionary(Of String, Object) From {
                {"@eq", c.Item1}, {"@ct", c.Item2}, {"@pn", c.Item3},
                {"@mn", c.Item4}, {"@mx", c.Item5}, {"@desc", c.Item6}
            }
            DatabaseHelper.ExecuteNonQueryInTransaction(conn, tx, sql, p)
        Next
    End Sub

End Class
