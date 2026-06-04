''' <summary>
''' 인증/권한 서비스
''' 로그인, 세션 관리, 권한 검사를 담당
''' </summary>
Public Class AuthService

    Private ReadOnly _userRepo As New UserRepository()

    ''' <summary>현재 로그인된 사용자 (세션)</summary>
    Public Shared Property CurrentUser As UserAccount

    ''' <summary>로그인 여부</summary>
    Public Shared ReadOnly Property IsLoggedIn As Boolean
        Get
            Return CurrentUser IsNot Nothing
        End Get
    End Property

    ''' <summary>사용자 인증 시도</summary>
    Public Function Login(userId As String, password As String) As UserAccount
        If String.IsNullOrWhiteSpace(userId) Then
            Throw New AuthenticationException("사용자 ID를 입력해 주세요.")
        End If
        If String.IsNullOrWhiteSpace(password) Then
            Throw New AuthenticationException("비밀번호를 입력해 주세요.")
        End If

        ' 평문을 그대로 넘긴다. 해시 비교가 아니라 저장된 해시와의 Verify(PBKDF2)로 검증한다.
        Dim user As UserAccount = _userRepo.Authenticate(userId, password)

        If user Is Nothing Then
            Throw New AuthenticationException("사용자 ID 또는 비밀번호가 올바르지 않습니다.")
        End If

        CurrentUser = user
        Return user
    End Function

    ''' <summary>로그아웃</summary>
    Public Sub Logout()
        CurrentUser = Nothing
    End Sub

    ''' <summary>
    ''' 권한 검사 (최소 필요 역할 이상인지)
    ''' Admin(1) > ProductionManager(2) > ViewOnly(3)
    ''' 숫자가 낮을수록 높은 권한
    ''' </summary>
    Public Shared Sub RequireRole(minimumRole As UserRole)
        If CurrentUser Is Nothing Then
            Throw New AuthenticationException("로그인이 필요합니다.")
        End If
        If CurrentUser.Role > minimumRole Then
            Throw New AuthorizationException(minimumRole, CurrentUser.Role)
        End If
    End Sub

    ''' <summary>현재 사용자가 특정 역할 이상인지 확인 (예외 없이)</summary>
    Public Shared Function HasPermission(minimumRole As UserRole) As Boolean
        If CurrentUser Is Nothing Then Return False
        Return CurrentUser.Role <= minimumRole
    End Function

    ''' <summary>현재 역할 표시 문자열</summary>
    Public Shared Function GetRoleDisplayName() As String
        If CurrentUser Is Nothing Then Return "미인증"
        Select Case CurrentUser.Role
            Case UserRole.Admin : Return "관리자"
            Case UserRole.ProductionManager : Return "생산관리자"
            Case UserRole.ViewOnly : Return "조회전용"
            Case Else : Return "알 수 없음"
        End Select
    End Function
End Class
