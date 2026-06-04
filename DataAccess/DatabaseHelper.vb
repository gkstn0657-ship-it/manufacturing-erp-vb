Imports System.Data.SQLite
Imports System.IO

''' <summary>
''' 데이터베이스 연결 및 초기화 헬퍼
''' 테이블 생성, 마이그레이션, 연결 관리를 담당
''' </summary>
Public NotInheritable Class DatabaseHelper

    Private Shared ReadOnly DbPath As String = "demo_erp.db"
    Public Shared ReadOnly ConnString As String = $"Data Source={DbPath};Version=3;Pooling=True;Max Pool Size=100;"

    Private Sub New()
    End Sub

    ''' <summary>새 DB 연결 생성</summary>
    Public Shared Function CreateConnection() As SQLiteConnection
        Return New SQLiteConnection(ConnString)
    End Function

    ''' <summary>DB 파일 존재 여부</summary>
    Public Shared Function DatabaseExists() As Boolean
        Return File.Exists(DbPath)
    End Function

    ''' <summary>DB 전체 삭제 (리셋용)</summary>
    Public Shared Sub DeleteDatabase()
        SQLiteConnection.ClearAllPools()
        If File.Exists(DbPath) Then File.Delete(DbPath)
    End Sub

    ''' <summary>전체 테이블 스키마 생성 (마이그레이션 포함)</summary>
    Public Shared Sub InitializeSchema()
        Using conn As SQLiteConnection = CreateConnection()
            conn.Open()
            Using cmd As SQLiteCommand = conn.CreateCommand()
                cmd.CommandText = "
                    -- 사용자 계정 마스터
                    CREATE TABLE IF NOT EXISTS USER_MASTER (
                        USER_ID TEXT PRIMARY KEY,
                        USER_NAME TEXT NOT NULL,
                        PASSWORD_HASH TEXT NOT NULL,
                        ROLE INTEGER NOT NULL DEFAULT 3,
                        IS_ACTIVE INTEGER NOT NULL DEFAULT 1,
                        LAST_LOGIN_AT TEXT,
                        CREATED_AT TEXT DEFAULT (datetime('now','localtime'))
                    );

                    -- 품목 마스터
                    CREATE TABLE IF NOT EXISTS ITEM_MASTER (
                        ITEM_CD TEXT PRIMARY KEY,
                        ITEM_NM TEXT NOT NULL,
                        ITEM_TYPE TEXT NOT NULL,
                        UNIT TEXT DEFAULT 'EA',
                        CREATED_AT TEXT DEFAULT (datetime('now','localtime'))
                    );

                    -- BOM 마스터
                    CREATE TABLE IF NOT EXISTS BOM_MASTER (
                        PARENT_CD TEXT,
                        CHILD_CD TEXT,
                        QTY REAL,
                        BOM_LEVEL INTEGER,
                        PRIMARY KEY (PARENT_CD, CHILD_CD)
                    );

                    -- 재고 마스터
                    CREATE TABLE IF NOT EXISTS STOCK_MASTER (
                        ITEM_CD TEXT,
                        WH_CD TEXT NOT NULL DEFAULT 'WH01',
                        CURRENT_QTY REAL DEFAULT 0,
                        SAFETY_QTY REAL DEFAULT 0,
                        UPDATED_AT TEXT DEFAULT (datetime('now','localtime')),
                        PRIMARY KEY (ITEM_CD, WH_CD)
                    );

                    -- 생산 실적 로그
                    CREATE TABLE IF NOT EXISTS PRODUCTION_LOG (
                        LOG_NO INTEGER PRIMARY KEY AUTOINCREMENT,
                        LOT_NO TEXT NOT NULL,
                        ITEM_CD TEXT NOT NULL,
                        PROD_QTY REAL,
                        WORK_DATE TEXT,
                        STATUS TEXT,
                        LINE_CD TEXT,
                        PROCESS_STEP TEXT,
                        CREATED_AT TEXT DEFAULT (datetime('now','localtime'))
                    );

                    -- 재고 변동 이력
                    CREATE TABLE IF NOT EXISTS STOCK_HISTORY (
                        HIST_NO INTEGER PRIMARY KEY AUTOINCREMENT,
                        LOG_NO INTEGER,
                        ITEM_CD TEXT,
                        WH_CD TEXT,
                        CHANGE_QTY REAL,
                        BEFORE_QTY REAL,
                        AFTER_QTY REAL,
                        CHANGE_TYPE TEXT,
                        CREATED_AT TEXT DEFAULT (datetime('now','localtime'))
                    );

                    -- 생산 라인 마스터
                    CREATE TABLE IF NOT EXISTS LINE_MASTER (
                        LINE_CD TEXT PRIMARY KEY,
                        LINE_NM TEXT NOT NULL,
                        PROCESS_STEP INTEGER NOT NULL,
                        TAKT_TIME INTEGER DEFAULT 60,
                        CAPACITY INTEGER DEFAULT 30,
                        IS_ACTIVE INTEGER DEFAULT 1
                    );

                    -- 설비 마스터
                    CREATE TABLE IF NOT EXISTS EQUIPMENT_MASTER (
                        EQUIP_CD TEXT PRIMARY KEY,
                        EQUIP_NM TEXT NOT NULL,
                        LINE_CD TEXT,
                        PROCESS_STEP INTEGER NOT NULL,
                        STATUS INTEGER DEFAULT 0,
                        LAST_MAINT_DATE TEXT,
                        CYCLE_COUNT INTEGER DEFAULT 0,
                        MAX_CYCLE_BEFORE_MAINT INTEGER DEFAULT 10000,
                        CURRENT_TEMP REAL DEFAULT 0,
                        TEMP_UPPER_LIMIT REAL DEFAULT 999,
                        TEMP_LOWER_LIMIT REAL DEFAULT 0,
                        ERROR_CODE TEXT
                    );

                    -- 설비 인터락 조건
                    CREATE TABLE IF NOT EXISTS INTERLOCK_CONDITION (
                        INTERLOCK_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        EQUIP_CD TEXT NOT NULL,
                        CONDITION_TYPE TEXT NOT NULL,
                        PARAM_NAME TEXT,
                        MIN_VALUE REAL,
                        MAX_VALUE REAL,
                        IS_ACTIVE INTEGER DEFAULT 1,
                        DESCRIPTION TEXT
                    );

                    -- LOT 추적 이력 (바코드 기반 Traceability)
                    CREATE TABLE IF NOT EXISTS LOT_TRACE (
                        LOT_NO TEXT NOT NULL,
                        BARCODE_SN TEXT,
                        ITEM_CD TEXT NOT NULL,
                        LINE_CD TEXT,
                        PROCESS_STEP INTEGER,
                        PROCESS_STATUS INTEGER DEFAULT 0,
                        START_TIME TEXT,
                        END_TIME TEXT,
                        WORKER_ID TEXT,
                        EQUIP_CD TEXT,
                        QUALITY_RESULT TEXT,
                        DEFECT_CODE TEXT,
                        PARENT_LOT_NO TEXT,
                        PRIMARY KEY (LOT_NO, PROCESS_STEP)
                    );

                    -- 생산 지시 (작업 오더)
                    CREATE TABLE IF NOT EXISTS PRODUCTION_ORDER (
                        ORDER_NO TEXT PRIMARY KEY,
                        ITEM_CD TEXT NOT NULL,
                        ORDER_QTY REAL NOT NULL,
                        PRODUCED_QTY REAL DEFAULT 0,
                        LINE_CD TEXT,
                        PLANNED_DATE TEXT,
                        STATUS INTEGER DEFAULT 0,
                        PRIORITY INTEGER DEFAULT 5,
                        CREATED_AT TEXT DEFAULT (datetime('now','localtime'))
                    );"
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>트랜잭션 내에서 단일 쿼리 실행</summary>
    Public Shared Function ExecuteScalarInTransaction(conn As SQLiteConnection, tx As SQLiteTransaction, sql As String, params As Dictionary(Of String, Object)) As Object
        Using cmd As New SQLiteCommand(sql, conn, tx)
            If params IsNot Nothing Then
                For Each kvp In params
                    cmd.Parameters.AddWithValue(kvp.Key, kvp.Value)
                Next
            End If
            Return cmd.ExecuteScalar()
        End Using
    End Function

    ''' <summary>트랜잭션 내에서 NonQuery 실행</summary>
    Public Shared Function ExecuteNonQueryInTransaction(conn As SQLiteConnection, tx As SQLiteTransaction, sql As String, params As Dictionary(Of String, Object)) As Integer
        Using cmd As New SQLiteCommand(sql, conn, tx)
            If params IsNot Nothing Then
                For Each kvp In params
                    cmd.Parameters.AddWithValue(kvp.Key, kvp.Value)
                Next
            End If
            Return cmd.ExecuteNonQuery()
        End Using
    End Function
End Class
