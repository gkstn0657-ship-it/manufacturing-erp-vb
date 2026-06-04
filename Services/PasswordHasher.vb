Imports System.Security.Cryptography
Imports System.Text

''' <summary>
''' 비밀번호 해싱 유틸리티 (PBKDF2 + per-user salt)
'''
''' 현업 표준에 맞춰 비밀번호를 저장한다.
'''  · SHA-256 단일 해시는 (1) 속도가 빨라 무차별 대입에 유리하고
'''    (2) 솔트가 없어 레인보우 테이블 공격에 취약하므로 비밀번호 저장에 부적합하다.
'''  · PBKDF2는 반복 횟수(iteration)로 의도적으로 느리게 만들고,
'''    사용자마다 다른 랜덤 솔트를 써서 같은 비밀번호도 매번 다른 해시가 나오게 한다.
'''
''' 저장 형식 (파라미터를 해시 문자열에 함께 보관 → 나중에 반복수 상향 등 마이그레이션 가능):
'''   PBKDF2$&lt;iterations&gt;$&lt;base64(salt)&gt;$&lt;base64(hash)&gt;
'''
''' .NET 표준 라이브러리(Rfc2898DeriveBytes)만 사용 → 외부 의존성 0.
''' 단일 EXE 배포(SelfContained)에 적합하다.
''' </summary>
Public NotInheritable Class PasswordHasher

    Private Sub New()
    End Sub

    ' OWASP 권장값 기준 (PBKDF2-HMAC-SHA256). 하드웨어 발전에 따라 상향 가능.
    Private Const Iterations As Integer = 600000
    Private Const SaltSize As Integer = 16   ' 128-bit
    Private Const HashSize As Integer = 32   ' 256-bit
    Private Const Prefix As String = "PBKDF2"

    ''' <summary>평문 비밀번호를 PBKDF2 해시 문자열로 변환 (저장용)</summary>
    Public Shared Function Hash(password As String) As String
        ' per-user 랜덤 솔트 생성
        Dim salt(SaltSize - 1) As Byte
        Using rng As RandomNumberGenerator = RandomNumberGenerator.Create()
            rng.GetBytes(salt)
        End Using

        Dim derived As Byte() = Derive(password, salt, Iterations)

        Return String.Join("$", New String() {
            Prefix,
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(derived)
        })
    End Function

    ''' <summary>
    ''' 평문 비밀번호가 저장된 해시와 일치하는지 검증.
    ''' PBKDF2 형식이면 정식 검증, 그 외(구형 SHA-256 64자리 hex)는 레거시 호환 검증.
    ''' </summary>
    Public Shared Function Verify(password As String, storedHash As String) As Boolean
        If String.IsNullOrEmpty(storedHash) Then Return False

        ' --- 신규 PBKDF2 형식 ---
        If storedHash.StartsWith(Prefix & "$") Then
            Dim parts = storedHash.Split("$"c)
            If parts.Length <> 4 Then Return False

            Dim iter As Integer
            If Not Integer.TryParse(parts(1), iter) Then Return False

            Dim salt As Byte() = Convert.FromBase64String(parts(2))
            Dim expected As Byte() = Convert.FromBase64String(parts(3))
            Dim actual As Byte() = Derive(password, salt, iter)

            Return FixedTimeEquals(actual, expected)
        End If

        ' --- 레거시 SHA-256(64자리 hex) 호환: 기존 DB가 있어도 로그인은 되게 ---
        If storedHash.Length = 64 Then
            Return FixedTimeEquals(
                Encoding.UTF8.GetBytes(LegacySha256(password)),
                Encoding.UTF8.GetBytes(storedHash))
        End If

        Return False
    End Function

    ''' <summary>저장된 해시가 구형 형식이라 재해싱이 필요한지 (로그인 성공 시 업그레이드용)</summary>
    Public Shared Function NeedsRehash(storedHash As String) As Boolean
        Return storedHash Is Nothing OrElse Not storedHash.StartsWith(Prefix & "$")
    End Function

    ' ── 내부 헬퍼 ────────────────────────────────────────────
    Private Shared Function Derive(password As String, salt As Byte(), iterations As Integer) As Byte()
        Using pbkdf2 As New Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)
            Return pbkdf2.GetBytes(HashSize)
        End Using
    End Function

    ''' <summary>구형 SHA-256 해시 (레거시 검증 전용)</summary>
    Private Shared Function LegacySha256(password As String) As String
        Using sha As SHA256 = SHA256.Create()
            Dim bytes As Byte() = sha.ComputeHash(Encoding.UTF8.GetBytes(password))
            Dim sb As New StringBuilder()
            For Each b In bytes
                sb.Append(b.ToString("x2"))
            Next
            Return sb.ToString()
        End Using
    End Function

    ''' <summary>타이밍 공격 방지를 위한 고정시간 비교</summary>
    Private Shared Function FixedTimeEquals(a As Byte(), b As Byte()) As Boolean
        If a Is Nothing OrElse b Is Nothing OrElse a.Length <> b.Length Then Return False
        Dim diff As Integer = 0
        For i = 0 To a.Length - 1
            diff = diff Or (a(i) Xor b(i))
        Next
        Return diff = 0
    End Function

End Class
