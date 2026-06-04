Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' 로그인 폼
''' 사용자 인증 및 역할 기반 접근 제어 진입점
''' </summary>
Public Class LoginForm
    Inherits Form

    Private txtUserId As TextBox
    Private txtPassword As TextBox
    Private btnLogin As Button
    Private lblTitle As Label
    Private lblSubtitle As Label
    Private lblMessage As Label
    Private lblUserInfo As Label
    Private pnlLogin As Panel

    Private ReadOnly _authService As New AuthService()

    Public Sub New()
        InitializeLoginUI()
    End Sub

    Private Sub InitializeLoginUI()
        ' 폼 기본 설정
        Me.Text = "DEMO ERP - 로그인"
        Me.Size = New Size(480, 420)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.FromArgb(15, 25, 35)

        ' 로그인 패널
        pnlLogin = New Panel() With {
            .Size = New Size(380, 320),
            .Location = New Point(40, 30),
            .BackColor = Color.FromArgb(24, 34, 50)
        }
        Me.Controls.Add(pnlLogin)

        ' 타이틀
        lblTitle = New Label() With {
            .Text = "DEMO ERP",
            .Font = New Font("맑은 고딕", 18, FontStyle.Bold),
            .ForeColor = Color.FromArgb(33, 150, 243),
            .TextAlign = ContentAlignment.MiddleCenter,
            .Location = New Point(0, 15),
            .Size = New Size(380, 40),
            .BackColor = Color.Transparent
        }
        pnlLogin.Controls.Add(lblTitle)

        ' 부제
        lblSubtitle = New Label() With {
            .Text = "자동차 제조 공정 최적화 통합 관리 시스템",
            .Font = New Font("맑은 고딕", 9),
            .ForeColor = Color.Gray,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Location = New Point(0, 55),
            .Size = New Size(380, 20),
            .BackColor = Color.Transparent
        }
        pnlLogin.Controls.Add(lblSubtitle)

        ' 사용자 ID
        Dim lblId As New Label() With {
            .Text = "사용자 ID",
            .Font = New Font("맑은 고딕", 9, FontStyle.Bold),
            .ForeColor = Color.LightGray,
            .Location = New Point(40, 95),
            .Size = New Size(300, 20),
            .BackColor = Color.Transparent
        }
        pnlLogin.Controls.Add(lblId)

        txtUserId = New TextBox() With {
            .Location = New Point(40, 118),
            .Size = New Size(300, 30),
            .Font = New Font("맑은 고딕", 11),
            .BackColor = Color.FromArgb(35, 45, 60),
            .ForeColor = Color.White,
            .BorderStyle = BorderStyle.FixedSingle
        }
        pnlLogin.Controls.Add(txtUserId)

        ' 비밀번호
        Dim lblPw As New Label() With {
            .Text = "비밀번호",
            .Font = New Font("맑은 고딕", 9, FontStyle.Bold),
            .ForeColor = Color.LightGray,
            .Location = New Point(40, 158),
            .Size = New Size(300, 20),
            .BackColor = Color.Transparent
        }
        pnlLogin.Controls.Add(lblPw)

        txtPassword = New TextBox() With {
            .Location = New Point(40, 181),
            .Size = New Size(300, 30),
            .Font = New Font("맑은 고딕", 11),
            .BackColor = Color.FromArgb(35, 45, 60),
            .ForeColor = Color.White,
            .BorderStyle = BorderStyle.FixedSingle,
            .PasswordChar = "*"c
        }
        pnlLogin.Controls.Add(txtPassword)

        ' 로그인 버튼
        btnLogin = New Button() With {
            .Text = "로그인",
            .Location = New Point(40, 228),
            .Size = New Size(300, 40),
            .Font = New Font("맑은 고딕", 11, FontStyle.Bold),
            .BackColor = Color.FromArgb(33, 150, 243),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnLogin.Click, AddressOf BtnLogin_Click
        pnlLogin.Controls.Add(btnLogin)

        ' 메시지 라벨
        lblMessage = New Label() With {
            .Text = "",
            .Font = New Font("맑은 고딕", 9),
            .ForeColor = Color.OrangeRed,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Location = New Point(40, 275),
            .Size = New Size(300, 20),
            .BackColor = Color.Transparent
        }
        pnlLogin.Controls.Add(lblMessage)

        ' 테스트 계정 안내
        lblUserInfo = New Label() With {
            .Text = "admin / admin123 (관리자)  |  prod_mgr / prod123 (생산관리자)  |  viewer / view123 (조회전용)",
            .Font = New Font("맑은 고딕", 7.5),
            .ForeColor = Color.FromArgb(100, 100, 100),
            .TextAlign = ContentAlignment.MiddleCenter,
            .Location = New Point(10, 360),
            .Size = New Size(450, 20),
            .BackColor = Color.Transparent
        }
        Me.Controls.Add(lblUserInfo)

        ' Enter 키 바인딩
        AddHandler txtPassword.KeyDown, Sub(s, ev)
                                             If ev.KeyCode = Keys.Enter Then
                                                 BtnLogin_Click(Nothing, Nothing)
                                                 ev.SuppressKeyPress = True
                                             End If
                                         End Sub

        AddHandler txtUserId.KeyDown, Sub(s, ev)
                                          If ev.KeyCode = Keys.Enter Then
                                              txtPassword.Focus()
                                              ev.SuppressKeyPress = True
                                          End If
                                      End Sub

        Me.AcceptButton = btnLogin
    End Sub

    Private Sub BtnLogin_Click(sender As Object, e As EventArgs)
        lblMessage.Text = ""
        lblMessage.ForeColor = Color.OrangeRed

        Try
            Dim user = _authService.Login(txtUserId.Text.Trim(), txtPassword.Text)
            lblMessage.ForeColor = Color.LimeGreen
            lblMessage.Text = $"{user.UserName} ({AuthService.GetRoleDisplayName()}) 로그인 성공"

            Me.DialogResult = DialogResult.OK
            Me.Close()
        Catch ex As AuthenticationException
            lblMessage.Text = ex.Message
            txtPassword.Clear()
            txtPassword.Focus()
        Catch ex As Exception
            lblMessage.Text = $"오류: {ex.Message}"
        End Try
    End Sub
End Class
