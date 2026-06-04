Imports System
Imports System.Collections.Generic

''' <summary>
''' DEMO ERP 도메인 엔티티 모델 정의
''' 자동차 제조업 특화 데이터 구조
''' </summary>

''' <summary>사용자 계정 정보</summary>
Public Class UserAccount
    Public Property UserId As String
    Public Property UserName As String
    Public Property PasswordHash As String
    Public Property Role As UserRole
    Public Property IsActive As Boolean = True
    Public Property LastLoginAt As String
    Public Property CreatedAt As String
End Class

''' <summary>품목 마스터</summary>
Public Class ItemMaster
    Public Property ItemCd As String
    Public Property ItemNm As String
    Public Property ItemType As String
    Public Property Unit As String = "EA"
    Public Property CreatedAt As String
End Class

''' <summary>BOM 노드 (소요량 전개용)</summary>
Public Class BomNode
    Public Property ItemCd As String
    Public Property UnitQty As Double
    Public Property ItemType As String
End Class

''' <summary>BOM 마스터 (부모-자식 관계)</summary>
Public Class BomMaster
    Public Property ParentCd As String
    Public Property ChildCd As String
    Public Property Qty As Double
    Public Property BomLevel As Integer
End Class

''' <summary>재고 마스터</summary>
Public Class StockMaster
    Public Property ItemCd As String
    Public Property WhCd As String = "WH01"
    Public Property CurrentQty As Double
    Public Property SafetyQty As Double
    Public Property UpdatedAt As String
End Class

''' <summary>재고 변동 이력</summary>
Public Class StockHistory
    Public Property HistNo As Long
    Public Property LogNo As Long
    Public Property ItemCd As String
    Public Property WhCd As String
    Public Property ChangeQty As Double
    Public Property BeforeQty As Double
    Public Property AfterQty As Double
    Public Property ChangeType As String
    Public Property CreatedAt As String
End Class

''' <summary>생산 실적 로그</summary>
Public Class ProductionLog
    Public Property LogNo As Long
    Public Property LotNo As String
    Public Property ItemCd As String
    Public Property ProdQty As Double
    Public Property WorkDate As String
    Public Property Status As String
    Public Property LineCd As String
    Public Property ProcessStep As String
    Public Property CreatedAt As String
End Class

''' <summary>
''' 생산 지시 (작업 오더)
''' 자동차 제조업 MES 연동 기준 정보
''' </summary>
Public Class ProductionOrder
    Public Property OrderNo As String
    Public Property ItemCd As String
    Public Property OrderQty As Double
    Public Property ProducedQty As Double
    Public Property LineCd As String
    Public Property PlannedDate As String
    Public Property Status As ProductionOrderStatus = ProductionOrderStatus.Planned
    Public Property Priority As Integer = 5
    Public Property CreatedAt As String
End Class

''' <summary>
''' 생산 라인 마스터
''' 자동차 공장 라인 정보 (차체 라인, 도장 라인, 조립 라인 등)
''' </summary>
Public Class LineMaster
    Public Property LineCd As String
    Public Property LineNm As String
    Public Property ProcessStep As ProcessStep
    Public Property TaktTime As Integer      ' 택트 타임 (초)
    Public Property Capacity As Integer       ' 시간당 생산 능력
    Public Property IsActive As Boolean = True
End Class

''' <summary>
''' 설비 마스터
''' 프레스/용접로봇/도장부스/조립공구 등
''' </summary>
Public Class EquipmentMaster
    Public Property EquipCd As String
    Public Property EquipNm As String
    Public Property LineCd As String
    Public Property ProcessStep As ProcessStep
    Public Property Status As EquipmentStatus = EquipmentStatus.Normal
    Public Property LastMaintenanceDate As String
    Public Property CycleCount As Long         ' 누적 작동 횟수
    Public Property MaxCycleBeforeMaint As Long ' 보전 주기
    Public Property CurrentTemp As Double      ' 현재 온도 (용접/도장)
    Public Property TempUpperLimit As Double   ' 온도 상한
    Public Property TempLowerLimit As Double   ' 온도 하한
    Public Property ErrorCode As String        ' 현재 에러 코드
End Class

''' <summary>
''' LOT 추적 정보
''' 바코드 기반 제품 이력 추적 (Traceability)
''' </summary>
Public Class LotTraceInfo
    Public Property LotNo As String
    Public Property BarcodeSn As String         ' 바코드 시리얼 넘버
    Public Property ItemCd As String
    Public Property LineCd As String
    Public Property ProcessStep As ProcessStep
    Public Property ProcessStatus As ProcessStatus
    Public Property StartTime As String
    Public Property EndTime As String
    Public Property WorkerId As String
    Public Property EquipCd As String
    Public Property QualityResult As String     ' OK / NG / REWORK
    Public Property DefectCode As String        ' 불량 코드
    Public Property ParentLotNo As String       ' 상위 LOT (조립 시)
End Class

''' <summary>
''' 설비 인터락 조건 정의
''' 공정 시작 전 설비 상태 점검 조건
''' </summary>
Public Class InterlockCondition
    Public Property InterlockId As Integer
    Public Property EquipCd As String
    Public Property ConditionType As String     ' TEMP_RANGE, CYCLE_LIMIT, STATUS_CHECK, MATERIAL_CHECK
    Public Property ParamName As String
    Public Property MinValue As Double
    Public Property MaxValue As Double
    Public Property IsActive As Boolean = True
    Public Property Description As String
End Class

''' <summary>인터락 체크 결과 상세</summary>
Public Class InterlockCheckResult
    Public Property EquipCd As String
    Public Property ConditionType As String
    Public Property Result As InterlockResult
    Public Property CurrentValue As Double
    Public Property Message As String
    Public Property CheckedAt As String
End Class

''' <summary>소요량 부족 상세</summary>
Public Class ShortageInfo
    Public Property ItemCd As String
    Public Property RequiredQty As Double
    Public Property CurrentQty As Double
    Public ReadOnly Property ShortageQty As Double
        Get
            Return RequiredQty - CurrentQty
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return $"[{ItemCd}] 필요:{RequiredQty}, 현재고:{CurrentQty} (부족량:{ShortageQty})"
    End Function
End Class
