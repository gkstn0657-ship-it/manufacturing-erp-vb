''' <summary>
''' DEMO ERP 시스템 열거형 정의
''' 자동차 제조 공정 전반에 사용되는 상태/타입/역할 코드
''' </summary>

''' <summary>사용자 역할 (로그인/권한관리)</summary>
Public Enum UserRole
    Admin = 1           ' 관리자: 전체 기능 접근
    ProductionManager = 2  ' 생산관리자: 생산/공정/설비 관리
    ViewOnly = 3        ' 조회전용: 데이터 조회만 가능
End Enum

''' <summary>품목 유형</summary>
Public Enum ItemType
    FG   ' 완제품 (Finished Goods)
    SFG  ' 반제품 (Semi-Finished Goods)
    RM   ' 원자재 (Raw Material)
End Enum

''' <summary>자동차 제조 공정 단계 (시퀀스 순서)</summary>
Public Enum ProcessStep
    Press = 10      ' 프레스 (강판 성형)
    Welding = 20    ' 용접 (차체 용접)
    Painting = 30   ' 도장 (전착/중도/상도)
    Assembly = 40   ' 조립 (트림/섀시/파이널)
    Inspection = 50 ' 검사 (품질 검사/출하 검사)
End Enum

''' <summary>공정 상태</summary>
Public Enum ProcessStatus
    Waiting     ' 대기
    Running     ' 가동 중
    Completed   ' 완료
    Blocked     ' 인터락 차단
    [Error]     ' 이상 발생
End Enum

''' <summary>설비 상태</summary>
Public Enum EquipmentStatus
    Normal      ' 정상 가동
    Warning     ' 경고 (파라미터 이탈)
    Fault       ' 고장
    Maintenance ' 보전 중
    Idle        ' 유휴
End Enum

''' <summary>재고 변동 유형</summary>
Public Enum StockChangeType
    Init        ' 초기값
    Production  ' 완제품 입고
    Backflush   ' 백플러시 차감
    Adjustment  ' 수동 조정
End Enum

''' <summary>생산 지시 상태</summary>
Public Enum ProductionOrderStatus
    Planned     ' 계획
    Released    ' 지시 확정
    InProcess   ' 공정 진행 중
    Completed   ' 완료
    Cancelled   ' 취소
End Enum

''' <summary>인터락 체크 결과</summary>
Public Enum InterlockResult
    Pass        ' 통과
    Fail        ' 차단
    Warning     ' 경고 (진행 가능)
End Enum
