Imports System

''' <summary>
''' DEMO ERP 커스텀 예외 계층
''' 비즈니스 규칙 위반 및 시스템 오류를 구분하여 처리
''' </summary>

''' <summary>ERP 기본 예외 (모든 커스텀 예외의 부모)</summary>
Public Class ErpException
    Inherits Exception

    Public Property ErrorCode As String

    Public Sub New(message As String, Optional errorCode As String = "ERR-000")
        MyBase.New(message)
        Me.ErrorCode = errorCode
    End Sub

    Public Sub New(message As String, innerException As Exception, Optional errorCode As String = "ERR-000")
        MyBase.New(message, innerException)
        Me.ErrorCode = errorCode
    End Sub
End Class

''' <summary>인증/권한 관련 예외</summary>
Public Class AuthenticationException
    Inherits ErpException

    Public Sub New(message As String)
        MyBase.New(message, "AUTH-001")
    End Sub
End Class

''' <summary>권한 부족 예외</summary>
Public Class AuthorizationException
    Inherits ErpException

    Public Property RequiredRole As UserRole
    Public Property CurrentRole As UserRole

    Public Sub New(requiredRole As UserRole, currentRole As UserRole)
        MyBase.New($"권한 부족: {requiredRole} 이상 필요 (현재: {currentRole})", "AUTH-002")
        Me.RequiredRole = requiredRole
        Me.CurrentRole = currentRole
    End Sub
End Class

''' <summary>재고 부족 예외 (Poka-Yoke 차단)</summary>
Public Class StockShortageException
    Inherits ErpException

    Public Property Shortages As List(Of ShortageInfo)

    Public Sub New(shortages As List(Of ShortageInfo))
        MyBase.New("하위 부품 소요량 부족으로 Poka-Yoke 차단", "STOCK-001")
        Me.Shortages = shortages
    End Sub
End Class

''' <summary>설비 인터락 차단 예외</summary>
Public Class InterlockException
    Inherits ErpException

    Public Property FailedChecks As List(Of InterlockCheckResult)

    Public Sub New(failedChecks As List(Of InterlockCheckResult))
        MyBase.New("설비 인터락 조건 미충족으로 공정 차단", "INTLK-001")
        Me.FailedChecks = failedChecks
    End Sub
End Class

''' <summary>공정 시퀀스 위반 예외</summary>
Public Class ProcessSequenceException
    Inherits ErpException

    Public Property CurrentStep As ProcessStep
    Public Property RequiredStep As ProcessStep

    Public Sub New(currentStep As ProcessStep, requiredStep As ProcessStep)
        MyBase.New($"공정 시퀀스 위반: 현재 {currentStep}, 선행 공정 {requiredStep} 미완료", "SEQ-001")
        Me.CurrentStep = currentStep
        Me.RequiredStep = requiredStep
    End Sub
End Class

''' <summary>데이터베이스 작업 예외</summary>
Public Class DatabaseException
    Inherits ErpException

    Public Sub New(message As String, innerException As Exception)
        MyBase.New($"데이터베이스 오류: {message}", innerException, "DB-001")
    End Sub
End Class

''' <summary>바코드/LOT 관련 예외</summary>
Public Class BarcodeException
    Inherits ErpException

    Public Property Barcode As String

    Public Sub New(barcode As String, message As String)
        MyBase.New(message, "BC-001")
        Me.Barcode = barcode
    End Sub
End Class
