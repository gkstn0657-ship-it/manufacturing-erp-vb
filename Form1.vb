Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.IO
Imports System.Windows.Forms
Imports System.Drawing

''' <summary>
''' DEMO ERP 메인 폼 (UI 계층)
''' 비즈니스 로직은 Services 계층에 위임, UI 이벤트 핸들링과 데이터 표시만 담당
''' 권한별 접근 제어 적용
''' </summary>
Public Class Form1

    ' ── Services (비즈니스 로직 위임) ──
    Private ReadOnly _productionService As New ProductionService()
    Private ReadOnly _authService As New AuthService()

    ' ── Repositories (데이터 조회 위임) ──
    Private ReadOnly _stockRepo As New StockRepository()
    Private ReadOnly _prodRepo As New ProductionRepository()
    Private ReadOnly _bomRepo As New BomRepository()
    Private ReadOnly _equipRepo As New EquipmentRepository()
    Private ReadOnly _lotRepo As New LotTraceRepository()
    Private ReadOnly _userRepo As New UserRepository()

    ' ── 동적 UI 컴포넌트 ──
    Private MainTabControl As TabControl
    Private TabDash, TabSim, TabSeq, TabBom, TabHist, TabEquip, TabProcess, TabItemMgr As TabPage

    ' 상단 상태 바
    Private pnlStatusBar As Panel
    Private lblUserInfo As Label
    Private lblSystemStatus As Label
    Private btnLogout As Button

    ' Dashboard 탭
    Private btnRefreshDash As Button
    Private btnResetSystem As Button
    Private btnItemManage As Button
    Private dgvDashboard As DataGridView
    Private lblDashStats As Label

    ' Production Simulator 탭
    Private btnProduceFg1 As Button
    Private btnProduceFg2 As Button
    Private btnProduceCab As Button
    Private txtSimLog As TextBox

    ' Sequence Test 탭
    Private btnRunSequence As Button
    Private txtSeqLog As TextBox

    ' BOM Explorer 탭
    Private cbBomSelector As ComboBox
    Private tvBomTree As TreeView

    ' 생산 실적 탭
    Private dgvProdLog As DataGridView
    Private dgvFullHistory As DataGridView

    ' 설비 현황 탭
    Private dgvEquipment As DataGridView
    Private dgvLines As DataGridView

    ' 공정 모니터링 탭
    Private dgvLotTrace As DataGridView
    Private btnRefreshTrace As Button

    ' 부품 관리 탭
    Private txtNewItemCd As TextBox
    Private txtNewItemNm As TextBox
    Private cbNewItemType As ComboBox
    Private nudNewInitQty As NumericUpDown
    Private nudNewSafetyQty As NumericUpDown
    Private btnRegisterItem As Button
    Private btnDeleteItem As Button
    Private lblItemResult As Label
    Private dgvItemList As DataGridView
    ' 재고 조정
    Private cbAdjustItem As ComboBox
    Private nudAdjustQty As NumericUpDown
    Private cbAdjustType As ComboBox
    Private btnAdjustStock As Button
    Private lblAdjustResult As Label
    ' BOM 관리
    Private cbBomParent As ComboBox
    Private cbBomChild As ComboBox
    Private nudBomQty As NumericUpDown
    Private btnAddBom As Button
    Private btnDeleteBom As Button
    Private lblBomResult As Label
    Private dgvBomList As DataGridView

    ' ══════════════════════════════════════
    ' 폼 로드
    ' ══════════════════════════════════════
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Text = "DEMO ERP - 자동차 제조 공정 최적화 통합 관리 시스템"
        Me.Size = New Size(1100, 820)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(15, 25, 35)

        Try
            ' 1. DB 초기화 및 시드 데이터
            DatabaseHelper.InitializeSchema()
            SeedDataManager.SeedAll()

            ' 2. 로그인 처리
            If Not ShowLoginDialog() Then
                Me.Close()
                Return
            End If

            ' 3. UI 빌드 (권한별)
            BuildStatusBar()
            BuildDynamicTabs()
            ApplyRolePermissions()

            ' 4. 초기 데이터 로드
            RefreshAllData()

            txtSimLog.AppendText("[시스템] DEMO ERP 가동 완료. 사용자: " &
                AuthService.CurrentUser.UserName & " (" & AuthService.GetRoleDisplayName() & ")" & Environment.NewLine)
        Catch ex As DatabaseException
            MessageBox.Show($"데이터베이스 초기화 오류: {ex.Message}", "DB 오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As ErpException
            MessageBox.Show($"시스템 초기화 오류: [{ex.ErrorCode}] {ex.Message}", "ERP 오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            MessageBox.Show($"시스템 초기화 중 예외 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>로그인 다이얼로그 표시</summary>
    Private Function ShowLoginDialog() As Boolean
        Using loginForm As New LoginForm()
            Dim result = loginForm.ShowDialog(Me)
            Return result = DialogResult.OK AndAlso AuthService.IsLoggedIn
        End Using
    End Function

    ' ══════════════════════════════════════
    ' 상단 사용자 정보 / 상태 바
    ' ══════════════════════════════════════
    Private Sub BuildStatusBar()
        pnlStatusBar = New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 36,
            .BackColor = Color.FromArgb(20, 30, 45)
        }
        Me.Controls.Add(pnlStatusBar)

        lblUserInfo = New Label() With {
            .Text = $"  [{AuthService.GetRoleDisplayName()}] {AuthService.CurrentUser.UserName} ({AuthService.CurrentUser.UserId})",
            .Font = New Font("맑은 고딕", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(33, 150, 243),
            .AutoSize = True,
            .Location = New Point(10, 9)
        }
        pnlStatusBar.Controls.Add(lblUserInfo)

        lblSystemStatus = New Label() With {
            .Text = "시스템 정상 가동",
            .Font = New Font("맑은 고딕", 9),
            .ForeColor = Color.LimeGreen,
            .AutoSize = True,
            .Location = New Point(400, 9)
        }
        pnlStatusBar.Controls.Add(lblSystemStatus)

        btnLogout = New Button() With {
            .Text = "로그아웃",
            .Size = New Size(80, 26),
            .Location = New Point(980, 4),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(100, 100, 100),
            .ForeColor = Color.White,
            .Font = New Font("맑은 고딕", 8)
        }
        AddHandler btnLogout.Click, AddressOf BtnLogout_Click
        pnlStatusBar.Controls.Add(btnLogout)
    End Sub

    ''' <summary>로그아웃 처리</summary>
    Private Sub BtnLogout_Click(sender As Object, e As EventArgs)
        If MessageBox.Show("로그아웃 하시겠습니까?", "로그아웃", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            _authService.Logout()
            ' 기존 컨트롤 제거
            Me.Controls.Clear()
            ' 다시 로그인
            If ShowLoginDialog() Then
                BuildStatusBar()
                BuildDynamicTabs()
                ApplyRolePermissions()
                RefreshAllData()
            Else
                Me.Close()
            End If
        End If
    End Sub

    ' ══════════════════════════════════════
    ' 권한별 UI 접근 제어
    ' ══════════════════════════════════════
    Private Sub ApplyRolePermissions()
        Dim role = AuthService.CurrentUser.Role

        Select Case role
            Case UserRole.ViewOnly
                ' 조회전용: 생산/관리 버튼 비활성화, 리셋 숨김
                btnProduceFg1.Enabled = False
                btnProduceFg2.Enabled = False
                btnProduceCab.Enabled = False
                btnRunSequence.Enabled = False
                btnItemManage.Enabled = False
                btnResetSystem.Visible = False

                ' 툴팁으로 안내
                Dim tip As New ToolTip()
                tip.SetToolTip(btnProduceFg1, "조회전용 계정은 생산 실행 권한이 없습니다")
                tip.SetToolTip(btnProduceFg2, "조회전용 계정은 생산 실행 권한이 없습니다")
                tip.SetToolTip(btnProduceCab, "조회전용 계정은 생산 실행 권한이 없습니다")
                tip.SetToolTip(btnRunSequence, "조회전용 계정은 생산 실행 권한이 없습니다")
                tip.SetToolTip(btnItemManage, "조회전용 계정은 부품 관리 권한이 없습니다")
                btnRegisterItem.Enabled = False
                btnDeleteItem.Enabled = False
                btnAdjustStock.Enabled = False
                btnAddBom.Enabled = False
                btnDeleteBom.Enabled = False

            Case UserRole.ProductionManager
                ' 생산관리자: 리셋 버튼 숨김 (관리자만 가능)
                btnResetSystem.Visible = False

            Case UserRole.Admin
                ' 관리자: 전체 기능 접근
        End Select
    End Sub

    ' ══════════════════════════════════════
    ' 동적 탭 빌드
    ' ══════════════════════════════════════
    Private Sub BuildDynamicTabs()
        MainTabControl = New TabControl()
        MainTabControl.Dock = DockStyle.Fill
        Me.Controls.Add(MainTabControl)
        MainTabControl.BringToFront()

        Dim tabBg As Color = Color.FromArgb(24, 34, 45)

        TabDash = New TabPage("Dashboard 현황") : TabDash.BackColor = tabBg
        TabSim = New TabPage("생산 시뮬레이터") : TabSim.BackColor = tabBg
        TabSeq = New TabPage("서열 지시 (M5)") : TabSeq.BackColor = tabBg
        TabBom = New TabPage("BOM Explorer") : TabBom.BackColor = tabBg
        TabHist = New TabPage("생산 실적/이력") : TabHist.BackColor = tabBg
        TabEquip = New TabPage("설비/라인 현황") : TabEquip.BackColor = tabBg
        TabProcess = New TabPage("공정 모니터링 (LOT)") : TabProcess.BackColor = tabBg
        TabItemMgr = New TabPage("부품 관리") : TabItemMgr.BackColor = tabBg

        MainTabControl.TabPages.AddRange(New TabPage() {TabDash, TabSim, TabSeq, TabBom, TabHist, TabEquip, TabProcess, TabItemMgr})

        BuildDashboardTab()
        BuildSimulatorTab()
        BuildSequenceTab()
        BuildBomTab()
        BuildHistoryTab()
        BuildEquipmentTab()
        BuildProcessMonitorTab()
        BuildItemManageTab()
    End Sub

    ' ── Dashboard 탭 ──
    Private Sub BuildDashboardTab()
        btnRefreshDash = New Button() With {.Text = "대시보드 새로고침", .Location = New Point(20, 20), .Size = New Size(160, 36), .BackColor = Color.FromArgb(33, 150, 243), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)}
        btnResetSystem = New Button() With {.Text = "전체 데이터 리셋", .Location = New Point(190, 20), .Size = New Size(150, 36), .BackColor = Color.FromArgb(244, 67, 54), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)}
        btnItemManage = New Button() With {.Text = "부품 추가/관리", .Location = New Point(350, 20), .Size = New Size(150, 36), .BackColor = Color.FromArgb(255, 152, 0), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)}
        lblDashStats = New Label() With {.Location = New Point(520, 20), .Size = New Size(520, 36), .Font = New Font("맑은 고딕", 10, FontStyle.Bold), .ForeColor = Color.LimeGreen, .TextAlign = ContentAlignment.MiddleLeft, .Text = "데이터 로드 중..."}

        Dim lblGuide As New Label() With {.Text = "팁: 품목을 더블 클릭하면 상세 재고 변동 이력 팝업이 표시됩니다.", .Location = New Point(20, 64), .Size = New Size(700, 20), .ForeColor = Color.LightSkyBlue, .Font = New Font("맑은 고딕", 8.5, FontStyle.Italic)}
        dgvDashboard = CreateGridView(New Point(20, 88), New Size(1040, 600))

        AddHandler btnRefreshDash.Click, Sub() RefreshAllData()
        AddHandler btnResetSystem.Click, AddressOf BtnResetSystem_Click
        AddHandler btnItemManage.Click, AddressOf BtnItemManage_Click
        AddHandler dgvDashboard.CellDoubleClick, AddressOf DgvDashboard_CellDoubleClick

        TabDash.Controls.AddRange(New Control() {btnRefreshDash, btnResetSystem, btnItemManage, lblDashStats, lblGuide, dgvDashboard})
    End Sub

    ' ── 생산 시뮬레이터 탭 ──
    Private Sub BuildSimulatorTab()
        Dim lblTitle As New Label() With {.Text = "[생산 시뮬레이터] 바코드 스캔 -> 인터락 체크 -> 공정 시퀀스 -> 백플러시", .Location = New Point(20, 15), .Size = New Size(700, 25), .Font = New Font("맑은 고딕", 10, FontStyle.Bold), .ForeColor = Color.White}

        btnProduceFg1 = New Button() With {.Text = "SP5 차체(스포티지) 생산", .Location = New Point(20, 50), .Size = New Size(210, 42), .BackColor = Color.FromArgb(76, 175, 80), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)}
        btnProduceFg2 = New Button() With {.Text = "NQ5 차체(스포티지 HEV) 생산", .Location = New Point(240, 50), .Size = New Size(230, 42), .BackColor = Color.FromArgb(0, 150, 136), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)}
        btnProduceCab = New Button() With {.Text = "봉고 캡 ASSY 생산", .Location = New Point(480, 50), .Size = New Size(200, 42), .BackColor = Color.FromArgb(139, 195, 74), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)}
        txtSimLog = New TextBox() With {.Location = New Point(20, 105), .Size = New Size(1040, 590), .Multiline = True, .ScrollBars = ScrollBars.Vertical, .BackColor = Color.Black, .ForeColor = Color.Lime, .Font = New Font("Consolas", 9.5), .ReadOnly = True}

        AddHandler btnProduceFg1.Click, Sub() RunProduction("FG-BODY-001", "LINE-FINAL-01")
        AddHandler btnProduceFg2.Click, Sub() RunProduction("FG-BODY-002", "LINE-FINAL-01")
        AddHandler btnProduceCab.Click, Sub() RunProduction("FG-CAB-001", "LINE-FINAL-01")

        TabSim.Controls.AddRange(New Control() {lblTitle, btnProduceFg1, btnProduceFg2, btnProduceCab, txtSimLog})
    End Sub

    ' ── 서열 지시 탭 ──
    Private Sub BuildSequenceTab()
        Dim lblTitle As New Label() With {.Text = "[M5 시나리오] 조립 라인 연속 서열 지시 통합 시뮬레이션 (20건)", .Location = New Point(20, 15), .Size = New Size(700, 25), .Font = New Font("맑은 고딕", 10, FontStyle.Bold), .ForeColor = Color.White}
        btnRunSequence = New Button() With {.Text = "연속 서열 지시 공정 가동 (M5)", .Location = New Point(20, 50), .Size = New Size(270, 42), .BackColor = Color.FromArgb(255, 152, 0), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 10, FontStyle.Bold)}
        txtSeqLog = New TextBox() With {.Location = New Point(20, 105), .Size = New Size(1040, 590), .Multiline = True, .ScrollBars = ScrollBars.Vertical, .BackColor = Color.Black, .ForeColor = Color.Cyan, .Font = New Font("Consolas", 9.5), .ReadOnly = True}

        AddHandler btnRunSequence.Click, AddressOf RunSequenceTest

        TabSeq.Controls.AddRange(New Control() {lblTitle, btnRunSequence, txtSeqLog})
    End Sub

    ' ── BOM Explorer 탭 ──
    Private Sub BuildBomTab()
        Dim lblTitle As New Label() With {.Text = "[BOM Explorer] 완제품 Multi-Level BOM 계층 구조 조회", .Location = New Point(20, 15), .Size = New Size(500, 25), .Font = New Font("맑은 고딕", 10, FontStyle.Bold), .ForeColor = Color.White}
        cbBomSelector = New ComboBox() With {.Location = New Point(20, 50), .Size = New Size(300, 28), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 10)}
        cbBomSelector.Items.AddRange(New String() {"FG-BODY-001 (기아 스포티지)", "FG-BODY-002 (기아 스포티지 HEV)", "FG-CAB-001 (봉고 캡)"})
        tvBomTree = New TreeView() With {.Location = New Point(20, 90), .Size = New Size(1040, 610), .BackColor = Color.FromArgb(30, 40, 55), .ForeColor = Color.White, .Font = New Font("Consolas", 10.5), .BorderStyle = BorderStyle.None}

        AddHandler cbBomSelector.SelectedIndexChanged, AddressOf CbBomSelector_Changed

        TabBom.Controls.AddRange(New Control() {lblTitle, cbBomSelector, tvBomTree})
    End Sub

    ' ── 생산 실적/이력 탭 ──
    Private Sub BuildHistoryTab()
        Dim lblProdTitle As New Label() With {.Text = "[상단] 완제품 공정 생산 완료 목록", .Location = New Point(20, 10), .Size = New Size(500, 20), .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold), .ForeColor = Color.FromArgb(0, 230, 118)}
        dgvProdLog = CreateGridView(New Point(20, 35), New Size(1040, 280))

        Dim lblHistTitle As New Label() With {.Text = "[하단] 원자재/반제품 백플러시 차감 상세 이력 (Stock Timeline)", .Location = New Point(20, 325), .Size = New Size(600, 20), .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold), .ForeColor = Color.LightSkyBlue}
        dgvFullHistory = CreateGridView(New Point(20, 350), New Size(1040, 340))

        TabHist.Controls.AddRange(New Control() {lblProdTitle, dgvProdLog, lblHistTitle, dgvFullHistory})
    End Sub

    ' ── 설비/라인 현황 탭 (신규) ──
    Private Sub BuildEquipmentTab()
        Dim lblLineTitle As New Label() With {.Text = "[생산 라인 현황] 프레스 -> 용접 -> 도장 -> 조립 -> 검사", .Location = New Point(20, 10), .Size = New Size(600, 20), .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold), .ForeColor = Color.FromArgb(255, 183, 77)}
        dgvLines = CreateGridView(New Point(20, 35), New Size(1040, 200))

        Dim lblEquipTitle As New Label() With {.Text = "[설비 상태 모니터링] 프레스기 / 용접 로봇 / 도장 부스 / 검사 장비", .Location = New Point(20, 245), .Size = New Size(600, 20), .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold), .ForeColor = Color.FromArgb(129, 212, 250)}
        dgvEquipment = CreateGridView(New Point(20, 270), New Size(1040, 420))

        TabEquip.Controls.AddRange(New Control() {lblLineTitle, dgvLines, lblEquipTitle, dgvEquipment})
    End Sub

    ' ── 공정 모니터링 탭 (신규) ──
    Private Sub BuildProcessMonitorTab()
        Dim lblTitle As New Label() With {.Text = "[LOT Traceability] 바코드 기반 공정 이력 추적", .Location = New Point(20, 10), .Size = New Size(600, 20), .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold), .ForeColor = Color.FromArgb(206, 147, 216)}
        btnRefreshTrace = New Button() With {.Text = "새로고침", .Location = New Point(920, 6), .Size = New Size(100, 28), .BackColor = Color.FromArgb(33, 150, 243), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9)}
        dgvLotTrace = CreateGridView(New Point(20, 38), New Size(1040, 650))

        AddHandler btnRefreshTrace.Click, Sub() LoadLotTraceData()
        AddHandler dgvLotTrace.CellDoubleClick, AddressOf DgvLotTrace_CellDoubleClick

        Dim lblGuide As New Label() With {.Text = "팁: LOT를 더블 클릭하면 해당 LOT의 전 공정 이력을 조회합니다.", .Location = New Point(200, 12), .Size = New Size(500, 18), .ForeColor = Color.Gray, .Font = New Font("맑은 고딕", 8)}

        TabProcess.Controls.AddRange(New Control() {lblTitle, lblGuide, btnRefreshTrace, dgvLotTrace})
    End Sub

    ' ── 부품 관리 탭 ──
    Private Sub BuildItemManageTab()
        ' ── 상단: 신규 품목 등록 영역 ──
        Dim lblRegTitle As New Label() With {
            .Text = "[신규 부품 등록]", .Font = New Font("맑은 고딕", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(255, 183, 77), .Location = New Point(20, 10), .Size = New Size(200, 22)
        }

        Dim lblCd As New Label() With {.Text = "품목코드", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(20, 40), .Size = New Size(60, 18)}
        txtNewItemCd = New TextBox() With {.Location = New Point(85, 37), .Size = New Size(140, 24), .Font = New Font("맑은 고딕", 9.5), .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White, .BorderStyle = BorderStyle.FixedSingle}

        Dim lblNm As New Label() With {.Text = "품목명", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(235, 40), .Size = New Size(50, 18)}
        txtNewItemNm = New TextBox() With {.Location = New Point(290, 37), .Size = New Size(200, 24), .Font = New Font("맑은 고딕", 9.5), .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White, .BorderStyle = BorderStyle.FixedSingle}

        Dim lblTp As New Label() With {.Text = "타입", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(500, 40), .Size = New Size(35, 18)}
        cbNewItemType = New ComboBox() With {.Location = New Point(540, 37), .Size = New Size(130, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}
        cbNewItemType.Items.AddRange(New String() {"RM - 원자재", "SFG - 반제품", "FG - 완제품"})
        cbNewItemType.SelectedIndex = 0

        Dim lblIq As New Label() With {.Text = "초기재고", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(680, 40), .Size = New Size(55, 18)}
        nudNewInitQty = New NumericUpDown() With {.Location = New Point(740, 37), .Size = New Size(80, 24), .Font = New Font("맑은 고딕", 9.5), .Maximum = 999999, .DecimalPlaces = 1, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        Dim lblSq As New Label() With {.Text = "안전재고", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(830, 40), .Size = New Size(55, 18)}
        nudNewSafetyQty = New NumericUpDown() With {.Location = New Point(890, 37), .Size = New Size(80, 24), .Font = New Font("맑은 고딕", 9.5), .Maximum = 999999, .DecimalPlaces = 1, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        ' 버튼: 등록 / 삭제
        btnRegisterItem = New Button() With {
            .Text = "부품 등록", .Location = New Point(20, 70), .Size = New Size(110, 32),
            .BackColor = Color.FromArgb(76, 175, 80), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnRegisterItem.Click, AddressOf BtnRegisterItem_Click

        btnDeleteItem = New Button() With {
            .Text = "선택 품목 삭제", .Location = New Point(140, 70), .Size = New Size(120, 32),
            .BackColor = Color.FromArgb(244, 67, 54), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnDeleteItem.Click, AddressOf BtnDeleteItem_Click

        lblItemResult = New Label() With {
            .Text = "", .ForeColor = Color.LimeGreen, .Font = New Font("맑은 고딕", 9),
            .Location = New Point(270, 76), .Size = New Size(400, 20)
        }

        ' 품목 목록 그리드
        Dim lblListTitle As New Label() With {
            .Text = "[등록된 전체 품목 목록] - 행 선택 후 '선택 품목 삭제' 가능",
            .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .ForeColor = Color.LightSkyBlue,
            .Location = New Point(20, 108), .Size = New Size(500, 18)
        }
        dgvItemList = CreateGridView(New Point(20, 130), New Size(1040, 180))

        ' ── 중단: 재고 수동 조정 영역 ──
        Dim lblAdjTitle As New Label() With {
            .Text = "[재고 수동 조정] 품목을 선택하고 입고(+) 또는 출고(-) 수량을 입력합니다.",
            .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .ForeColor = Color.FromArgb(255, 183, 77),
            .Location = New Point(20, 318), .Size = New Size(600, 18)
        }

        Dim lblAdjItem As New Label() With {.Text = "품목", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(20, 343), .Size = New Size(35, 18)}
        cbAdjustItem = New ComboBox() With {.Location = New Point(58, 340), .Size = New Size(280, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}

        Dim lblAdjQty As New Label() With {.Text = "수량", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(348, 343), .Size = New Size(35, 18)}
        nudAdjustQty = New NumericUpDown() With {.Location = New Point(386, 340), .Size = New Size(100, 24), .Font = New Font("맑은 고딕", 9.5), .Maximum = 999999, .Minimum = -999999, .DecimalPlaces = 1, .Increment = 10, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        Dim lblAdjTp As New Label() With {.Text = "사유", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(496, 343), .Size = New Size(35, 18)}
        cbAdjustType = New ComboBox() With {.Location = New Point(534, 340), .Size = New Size(150, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}
        cbAdjustType.Items.AddRange(New String() {"RECEIVE - 입고", "SHIP - 출고", "ADJUST - 재고조정", "RETURN - 반품"})
        cbAdjustType.SelectedIndex = 0

        btnAdjustStock = New Button() With {
            .Text = "재고 반영", .Location = New Point(694, 337), .Size = New Size(100, 28),
            .BackColor = Color.FromArgb(0, 150, 136), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnAdjustStock.Click, AddressOf BtnAdjustStock_Click

        lblAdjustResult = New Label() With {
            .Text = "", .ForeColor = Color.LimeGreen, .Font = New Font("맑은 고딕", 9),
            .Location = New Point(800, 343), .Size = New Size(260, 18)
        }

        ' ── 하단: BOM 구성 관리 영역 ──
        Dim lblBomTitle As New Label() With {
            .Text = "[BOM 구성 관리] 부모 품목에 자식 부품을 연결합니다.",
            .Font = New Font("맑은 고딕", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(129, 212, 250), .Location = New Point(20, 375), .Size = New Size(500, 22)
        }

        Dim lblPar As New Label() With {.Text = "부모 품목", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(20, 403), .Size = New Size(65, 18)}
        cbBomParent = New ComboBox() With {.Location = New Point(90, 400), .Size = New Size(300, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}
        AddHandler cbBomParent.SelectedIndexChanged, Sub() LoadBomChildrenGrid()

        Dim lblChi As New Label() With {.Text = "자식 부품", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(400, 403), .Size = New Size(65, 18)}
        cbBomChild = New ComboBox() With {.Location = New Point(470, 400), .Size = New Size(300, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}

        Dim lblBq As New Label() With {.Text = "소요량", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9), .Location = New Point(780, 403), .Size = New Size(45, 18)}
        nudBomQty = New NumericUpDown() With {.Location = New Point(830, 400), .Size = New Size(80, 24), .Font = New Font("맑은 고딕", 9.5), .Maximum = 99999, .Minimum = 0.1D, .Value = 1, .DecimalPlaces = 2, .Increment = 0.5D, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        btnAddBom = New Button() With {
            .Text = "BOM 추가", .Location = New Point(920, 398), .Size = New Size(90, 28),
            .BackColor = Color.FromArgb(76, 175, 80), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnAddBom.Click, AddressOf BtnAddBom_Click

        btnDeleteBom = New Button() With {
            .Text = "선택 BOM 삭제", .Location = New Point(20, 430), .Size = New Size(120, 28),
            .BackColor = Color.FromArgb(244, 67, 54), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnDeleteBom.Click, AddressOf BtnDeleteBom_Click

        lblBomResult = New Label() With {
            .Text = "", .ForeColor = Color.LimeGreen, .Font = New Font("맑은 고딕", 9),
            .Location = New Point(150, 435), .Size = New Size(400, 18)
        }

        dgvBomList = CreateGridView(New Point(20, 462), New Size(1040, 230))

        TabItemMgr.Controls.AddRange(New Control() {
            lblRegTitle, lblCd, txtNewItemCd, lblNm, txtNewItemNm, lblTp, cbNewItemType,
            lblIq, nudNewInitQty, lblSq, nudNewSafetyQty,
            btnRegisterItem, btnDeleteItem, lblItemResult, lblListTitle, dgvItemList,
            lblAdjTitle, lblAdjItem, cbAdjustItem, lblAdjQty, nudAdjustQty, lblAdjTp, cbAdjustType, btnAdjustStock, lblAdjustResult,
            lblBomTitle, lblPar, cbBomParent, lblChi, cbBomChild, lblBq, nudBomQty,
            btnAddBom, btnDeleteBom, lblBomResult, dgvBomList
        })

        ' 초기 데이터 로드
        LoadItemManageData()
    End Sub

    ' ══════════════════════════════════════
    ' 이벤트 핸들러 (비즈니스 로직은 서비스에 위임)
    ' ══════════════════════════════════════

    ''' <summary>단건 생산 실행</summary>
    Private Sub RunProduction(itemCd As String, lineCd As String)
        Try
            AuthService.RequireRole(UserRole.ProductionManager)

            txtSimLog.AppendText(Environment.NewLine & New String("="c, 70) & Environment.NewLine)
            Dim logger As Action(Of String) = Sub(msg)
                                                  txtSimLog.AppendText(msg & Environment.NewLine)
                                              End Sub

            Dim success = _productionService.ExecuteProduction(itemCd, 1.0, lineCd, logger)
            If success Then RefreshAllData()

        Catch ex As AuthorizationException
            MessageBox.Show($"권한 부족: {AuthService.GetRoleDisplayName()} 계정은 생산 실행 권한이 없습니다.", "접근 거부", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As ErpException
            MessageBox.Show($"[{ex.ErrorCode}] {ex.Message}", "ERP 오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            MessageBox.Show($"예기치 않은 오류: {ex.Message}", "시스템 오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>연속 서열 지시 시뮬레이션</summary>
    Private Sub RunSequenceTest(sender As Object, e As EventArgs)
        Try
            AuthService.RequireRole(UserRole.ProductionManager)

            txtSeqLog.Clear()
            BarcodeService.ResetSequence()
            txtSeqLog.AppendText("=" & New String("="c, 69) & Environment.NewLine)
            txtSeqLog.AppendText(" [M5] 조립 라인 연속 서열 지시 시뮬레이션 (인터락 + 공정 시퀀스 + 백플러시)" & Environment.NewLine)
            txtSeqLog.AppendText("=" & New String("="c, 69) & Environment.NewLine)

            Dim total As Integer = 20
            Dim rand As New Random()
            Dim success As Integer = 0
            Dim fail As Integer = 0

            For i As Integer = 1 To total
                Dim rVal As Double = rand.NextDouble()
                Dim target As String
                If rVal < 0.6 Then
                    target = "FG-BODY-001"
                ElseIf rVal < 0.85 Then
                    target = "FG-BODY-002"
                Else
                    target = "FG-CAB-001"
                End If

                txtSeqLog.AppendText(Environment.NewLine & $"[{i:D2}/{total}] 서열 큐 -> 대상: {target}" & Environment.NewLine)

                Dim logger As Action(Of String) = Sub(msg)
                                                      txtSeqLog.AppendText(msg & Environment.NewLine)
                                                  End Sub

                If _productionService.ExecuteProduction(target, 1.0, "LINE-FINAL-01", logger) Then
                    success += 1
                Else
                    fail += 1
                End If
            Next

            RefreshAllData()
            txtSeqLog.AppendText(Environment.NewLine & New String("="c, 50) & Environment.NewLine)
            txtSeqLog.AppendText($"총 서열: {total}건 | 완료: {success}건 | 차단(Rollback): {fail}건" & Environment.NewLine)
            txtSeqLog.AppendText($"트랜잭션 정합성: {(CDbl(success) / total * 100):F1}%" & Environment.NewLine)

        Catch ex As AuthorizationException
            MessageBox.Show("권한 부족: 생산관리자 이상만 서열 지시를 실행할 수 있습니다.", "접근 거부", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show($"서열 시뮬레이션 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>부품 관리 팝업 열기</summary>
    Private Sub BtnItemManage_Click(sender As Object, e As EventArgs)
        Try
            AuthService.RequireRole(UserRole.ProductionManager)

            Using frm As New ItemManageForm()
                frm.OnDataChanged = Sub() RefreshAllData()
                frm.ShowDialog(Me)
            End Using

            ' 폼 닫힌 후 최종 새로고침
            RefreshAllData()
        Catch ex As AuthorizationException
            MessageBox.Show("권한 부족: 생산관리자 이상만 부품 관리가 가능합니다.", "접근 거부", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show($"부품 관리 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>전체 데이터 리셋</summary>
    Private Sub BtnResetSystem_Click(sender As Object, e As EventArgs)
        Try
            AuthService.RequireRole(UserRole.Admin)

            If MessageBox.Show("모든 데이터를 초기 상태로 리셋하시겠습니까?" & Environment.NewLine & "(관리자 전용 기능)", "ERP 초기화 경고", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
                DatabaseHelper.DeleteDatabase()
                DatabaseHelper.InitializeSchema()
                SeedDataManager.SeedAll()
                BarcodeService.ResetSequence()
                RefreshAllData()
                txtSimLog.Clear()
                txtSeqLog.Clear()
                MessageBox.Show("데이터베이스가 초기 상태로 리셋되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

        Catch ex As AuthorizationException
            MessageBox.Show("관리자만 데이터 리셋이 가능합니다.", "접근 거부", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show($"리셋 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>BOM 트리뷰 로드</summary>
    Private Sub CbBomSelector_Changed(sender As Object, e As EventArgs)
        If cbBomSelector.SelectedIndex < 0 Then Return
        Dim itemCd As String = cbBomSelector.SelectedItem.ToString().Split(" "c)(0)

        Try
            tvBomTree.Nodes.Clear()
            Dim rootNode As New TreeNode($"{itemCd} [최종 조립 완제품]")
            tvBomTree.Nodes.Add(rootNode)

            Using conn As New SQLiteConnection(DatabaseHelper.ConnString)
                conn.Open()
                BuildBomTreeRecursive(conn, itemCd, rootNode, 1)
            End Using
            tvBomTree.ExpandAll()
        Catch ex As Exception
            MessageBox.Show($"BOM 조회 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BuildBomTreeRecursive(conn As SQLiteConnection, parentCd As String, uNode As TreeNode, level As Integer)
        Dim items = _bomRepo.GetBomChildren(conn, parentCd)
        For Each itm In items
            Dim typeStr As String = If(itm.Item4 = "SFG", "반제품", "원자재")
            Dim cNode As New TreeNode($"L{level} : {itm.Item1} ({itm.Item3}) - 소요: {itm.Item2}EA [{typeStr}]")
            uNode.Nodes.Add(cNode)
            If itm.Item4 = "SFG" Then
                BuildBomTreeRecursive(conn, itm.Item1, cNode, level + 1)
            End If
        Next
    End Sub

    ''' <summary>재고 상세 이력 팝업</summary>
    Private Sub DgvDashboard_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        Try
            Dim itemCd As String = dgvDashboard.Rows(e.RowIndex).Cells("품목코드").Value.ToString()
            Dim dt = _stockRepo.GetStockHistory(itemCd)

            Dim popForm As New Form() With {
                .Text = $"부품 상세 이력 추적 - {itemCd}",
                .Size = New Size(750, 450),
                .StartPosition = FormStartPosition.CenterParent,
                .BackColor = Color.FromArgb(24, 34, 45)
            }
            Dim popGrid = CreateGridView(New Point(0, 0), popForm.ClientSize)
            popGrid.Dock = DockStyle.Fill
            popGrid.DataSource = dt
            popForm.Controls.Add(popGrid)
            popForm.ShowDialog()
        Catch ex As Exception
            MessageBox.Show($"이력 조회 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>LOT 공정 이력 팝업</summary>
    Private Sub DgvLotTrace_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        Try
            Dim lotNo As String = dgvLotTrace.Rows(e.RowIndex).Cells("LOT번호").Value.ToString()
            Dim dt = _lotRepo.GetLotTraceHistory(lotNo)

            Dim popForm As New Form() With {
                .Text = $"LOT 공정 이력 추적 - {lotNo}",
                .Size = New Size(900, 400),
                .StartPosition = FormStartPosition.CenterParent,
                .BackColor = Color.FromArgb(24, 34, 45)
            }
            Dim popGrid = CreateGridView(New Point(0, 0), popForm.ClientSize)
            popGrid.Dock = DockStyle.Fill
            popGrid.DataSource = dt
            popForm.Controls.Add(popGrid)
            popForm.ShowDialog()
        Catch ex As Exception
            MessageBox.Show($"LOT 이력 조회 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ══════════════════════════════════════
    ' 데이터 로드 / 새로고침
    ' ══════════════════════════════════════
    Private Sub RefreshAllData()
        Try
            LoadDashboardData()
            LoadProductionLog()
            LoadEquipmentData()
            LoadLotTraceData()
            LoadItemManageData()
        Catch ex As Exception
            lblSystemStatus.Text = $"데이터 로드 오류: {ex.Message}"
            lblSystemStatus.ForeColor = Color.OrangeRed
        End Try
    End Sub

    Private Sub LoadDashboardData()
        Try
            ' 재고 현황
            dgvDashboard.DataSource = _stockRepo.GetDashboardStock()
            dgvDashboard.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

            ' 안전재고 하이라이팅
            For Each row As DataGridViewRow In dgvDashboard.Rows
                If row.Cells("현재재고").Value IsNot DBNull.Value AndAlso row.Cells("안전재고").Value IsNot DBNull.Value Then
                    Dim cur = Convert.ToDouble(row.Cells("현재재고").Value)
                    Dim saf = Convert.ToDouble(row.Cells("안전재고").Value)
                    If cur < saf Then
                        row.Cells("현재재고").Style.BackColor = Color.MistyRose
                        row.Cells("현재재고").Style.ForeColor = Color.Red
                    ElseIf cur < saf * 1.5 Then
                        row.Cells("현재재고").Style.BackColor = Color.LightYellow
                        row.Cells("현재재고").Style.ForeColor = Color.DarkOrange
                    End If
                End If
            Next

            ' KPI
            Dim kpi = _prodRepo.GetProductionKpi()
            lblDashStats.Text = $"[실시간 KPI] 총 조립 실적: {kpi.Item1}건 | 누적 생산량: {kpi.Item2}대"
        Catch ex As Exception
            ' 대시보드 로드 오류는 라벨로 표시
            lblDashStats.Text = $"데이터 로드 오류: {ex.Message}"
            lblDashStats.ForeColor = Color.OrangeRed
        End Try
    End Sub

    Private Sub LoadProductionLog()
        Try
            dgvProdLog.DataSource = _prodRepo.GetProductionLog()
            dgvProdLog.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

            dgvFullHistory.DataSource = _stockRepo.GetFullStockHistory()
            dgvFullHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        Catch ex As Exception
            ' 무시 - 아직 데이터 없을 수 있음
        End Try
    End Sub

    Private Sub LoadEquipmentData()
        Try
            dgvLines.DataSource = _equipRepo.GetLineDashboard()
            dgvLines.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

            dgvEquipment.DataSource = _equipRepo.GetEquipmentDashboard()
            dgvEquipment.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

            ' 설비 상태 하이라이팅
            For Each row As DataGridViewRow In dgvEquipment.Rows
                If row.Cells("상태").Value IsNot Nothing Then
                    Dim status = row.Cells("상태").Value.ToString()
                    Select Case status
                        Case "고장"
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200)
                            row.DefaultCellStyle.ForeColor = Color.DarkRed
                        Case "경고"
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 200)
                            row.DefaultCellStyle.ForeColor = Color.DarkOrange
                        Case "보전중"
                            row.DefaultCellStyle.BackColor = Color.FromArgb(200, 220, 255)
                            row.DefaultCellStyle.ForeColor = Color.DarkBlue
                    End Select
                End If
            Next
        Catch ex As Exception
            ' 무시
        End Try
    End Sub

    Private Sub LoadLotTraceData()
        Try
            dgvLotTrace.DataSource = _lotRepo.GetRecentLotTrace()
            dgvLotTrace.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        Catch ex As Exception
            ' 무시
        End Try
    End Sub

    ' ══════════════════════════════════════
    ' 부품 관리 탭 - 이벤트 핸들러
    ' ══════════════════════════════════════

    ''' <summary>부품 관리 탭 데이터 로드</summary>
    Private Sub LoadItemManageData()
        Try
            ' 품목 목록 그리드
            dgvItemList.DataSource = _stockRepo.GetDashboardStock()
            dgvItemList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

            ' 콤보박스 갱신
            Dim dt = _userRepo.GetAllItemList()
            cbBomParent.Items.Clear()
            cbBomChild.Items.Clear()
            cbAdjustItem.Items.Clear()
            For Each row As DataRow In dt.Rows
                Dim display = row("ITEM_CD").ToString() & " (" & row("ITEM_NM").ToString() & ")"
                cbBomParent.Items.Add(display)
                cbBomChild.Items.Add(display)
                cbAdjustItem.Items.Add(display)
            Next
        Catch ex As Exception
            ' 무시
        End Try
    End Sub

    ''' <summary>재고 수동 조정</summary>
    Private Sub BtnAdjustStock_Click(sender As Object, e As EventArgs)
        lblAdjustResult.ForeColor = Color.OrangeRed

        Try
            AuthService.RequireRole(UserRole.ProductionManager)
        Catch ex As AuthorizationException
            lblAdjustResult.Text = "권한 부족: 생산관리자 이상만 조정 가능"
            Return
        End Try

        If cbAdjustItem.SelectedIndex < 0 Then
            lblAdjustResult.Text = "품목을 선택하세요."
            Return
        End If
        If nudAdjustQty.Value = 0 Then
            lblAdjustResult.Text = "수량을 입력하세요. (양수=입고, 음수=출고)"
            Return
        End If

        Dim itemCd = cbAdjustItem.SelectedItem.ToString().Split(" "c)(0)
        Dim adjustQty = CDbl(nudAdjustQty.Value)
        Dim changeType = cbAdjustType.SelectedItem.ToString().Split(" "c)(0)

        Try
            _stockRepo.AdjustStock(itemCd, adjustQty, changeType)
            lblAdjustResult.ForeColor = Color.LimeGreen
            lblAdjustResult.Text = itemCd & " 재고 " & If(adjustQty > 0, "+" & adjustQty.ToString(), adjustQty.ToString()) & " 반영 완료"
            nudAdjustQty.Value = 0
            LoadItemManageData()
            LoadDashboardData()
        Catch ex As StockShortageException
            lblAdjustResult.Text = "재고 부족: 현재고보다 많이 출고할 수 없습니다."
        Catch ex As Exception
            lblAdjustResult.Text = "오류: " & ex.Message
        End Try
    End Sub

    ''' <summary>신규 부품 등록</summary>
    Private Sub BtnRegisterItem_Click(sender As Object, e As EventArgs)
        lblItemResult.ForeColor = Color.OrangeRed

        Try
            AuthService.RequireRole(UserRole.ProductionManager)
        Catch ex As AuthorizationException
            lblItemResult.Text = "권한 부족: 생산관리자 이상만 등록 가능"
            Return
        End Try

        Dim itemCd = txtNewItemCd.Text.Trim().ToUpper()
        Dim itemNm = txtNewItemNm.Text.Trim()

        If String.IsNullOrEmpty(itemCd) Then
            lblItemResult.Text = "품목 코드를 입력하세요."
            Return
        End If
        If String.IsNullOrEmpty(itemNm) Then
            lblItemResult.Text = "품목명을 입력하세요."
            Return
        End If
        If cbNewItemType.SelectedIndex < 0 Then
            lblItemResult.Text = "품목 타입을 선택하세요."
            Return
        End If

        Dim itemType As String = cbNewItemType.SelectedItem.ToString().Split(" "c)(0)

        If _stockRepo.ItemExists(itemCd) Then
            lblItemResult.Text = $"이미 존재하는 품목 코드: {itemCd}"
            Return
        End If

        Try
            _stockRepo.RegisterNewItem(itemCd, itemNm, itemType, CDbl(nudNewInitQty.Value), CDbl(nudNewSafetyQty.Value))
            lblItemResult.ForeColor = Color.LimeGreen
            lblItemResult.Text = $"{itemCd} ({itemNm}) 등록 완료!"

            txtNewItemCd.Clear()
            txtNewItemNm.Clear()
            nudNewInitQty.Value = 0
            nudNewSafetyQty.Value = 0
            txtNewItemCd.Focus()

            LoadItemManageData()
            LoadDashboardData()
        Catch ex As Exception
            lblItemResult.Text = $"등록 오류: {ex.Message}"
        End Try
    End Sub

    ''' <summary>선택 품목 삭제</summary>
    Private Sub BtnDeleteItem_Click(sender As Object, e As EventArgs)
        lblItemResult.ForeColor = Color.OrangeRed

        Try
            AuthService.RequireRole(UserRole.Admin)
        Catch ex As AuthorizationException
            lblItemResult.Text = "권한 부족: 관리자만 삭제 가능"
            Return
        End Try

        If dgvItemList.CurrentRow Is Nothing Then
            lblItemResult.Text = "삭제할 품목을 선택하세요."
            Return
        End If

        Dim itemCd = dgvItemList.CurrentRow.Cells("품목코드").Value.ToString()
        Dim itemNm = dgvItemList.CurrentRow.Cells("품목명").Value.ToString()

        If MessageBox.Show($"'{itemCd} ({itemNm})' 품목을 삭제합니까?" & vbCrLf & "관련 BOM, 재고, 이력이 모두 삭제됩니다.",
                           "품목 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                _stockRepo.DeleteItem(itemCd)
                lblItemResult.ForeColor = Color.LimeGreen
                lblItemResult.Text = $"{itemCd} 삭제 완료"
                LoadItemManageData()
                LoadDashboardData()
            Catch ex As Exception
                lblItemResult.Text = $"삭제 오류: {ex.Message}"
            End Try
        End If
    End Sub

    ''' <summary>BOM 추가</summary>
    Private Sub BtnAddBom_Click(sender As Object, e As EventArgs)
        lblBomResult.ForeColor = Color.OrangeRed

        Try
            AuthService.RequireRole(UserRole.ProductionManager)
        Catch ex As AuthorizationException
            lblBomResult.Text = "권한 부족"
            Return
        End Try

        If cbBomParent.SelectedIndex < 0 Then
            lblBomResult.Text = "부모 품목을 선택하세요."
            Return
        End If
        If cbBomChild.SelectedIndex < 0 Then
            lblBomResult.Text = "자식 부품을 선택하세요."
            Return
        End If

        Dim parentCd = cbBomParent.SelectedItem.ToString().Split(" "c)(0)
        Dim childCd = cbBomChild.SelectedItem.ToString().Split(" "c)(0)

        If parentCd = childCd Then
            lblBomResult.Text = "부모와 자식이 같을 수 없습니다."
            Return
        End If

        Try
            _stockRepo.RegisterBomLink(parentCd, childCd, CDbl(nudBomQty.Value))
            lblBomResult.ForeColor = Color.LimeGreen
            lblBomResult.Text = $"{parentCd} -> {childCd} BOM 추가 완료"
            LoadBomChildrenGrid()
        Catch ex As Exception
            lblBomResult.Text = $"BOM 추가 오류: {ex.Message}"
        End Try
    End Sub

    ''' <summary>선택 BOM 삭제</summary>
    Private Sub BtnDeleteBom_Click(sender As Object, e As EventArgs)
        lblBomResult.ForeColor = Color.OrangeRed

        Try
            AuthService.RequireRole(UserRole.Admin)
        Catch ex As AuthorizationException
            lblBomResult.Text = "권한 부족: 관리자만 삭제 가능"
            Return
        End Try

        If cbBomParent.SelectedIndex < 0 OrElse dgvBomList.CurrentRow Is Nothing Then
            lblBomResult.Text = "삭제할 BOM 행을 선택하세요."
            Return
        End If

        Dim parentCd = cbBomParent.SelectedItem.ToString().Split(" "c)(0)
        Dim childCd = dgvBomList.CurrentRow.Cells("자식품목코드").Value.ToString()

        If MessageBox.Show($"BOM 삭제: {parentCd} -> {childCd}", "BOM 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            Try
                _stockRepo.DeleteBomLink(parentCd, childCd)
                lblBomResult.ForeColor = Color.LimeGreen
                lblBomResult.Text = "BOM 삭제 완료"
                LoadBomChildrenGrid()
            Catch ex As Exception
                lblBomResult.Text = $"삭제 오류: {ex.Message}"
            End Try
        End If
    End Sub

    ''' <summary>선택된 부모 품목의 BOM 자식 목록 로드</summary>
    Private Sub LoadBomChildrenGrid()
        If cbBomParent.SelectedIndex < 0 Then Return
        Try
            Dim parentCd = cbBomParent.SelectedItem.ToString().Split(" "c)(0)
            dgvBomList.DataSource = _stockRepo.GetBomChildrenTable(parentCd)
            dgvBomList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        Catch ex As Exception
            ' 무시
        End Try
    End Sub

    ' ══════════════════════════════════════
    ' 유틸리티
    ' ══════════════════════════════════════
    Private Function CreateGridView(loc As Point, sz As Size) As DataGridView
        Return New DataGridView() With {
            .Location = loc,
            .Size = sz,
            .ReadOnly = True,
            .AllowUserToAddRows = False,
            .BackgroundColor = Color.FromArgb(30, 40, 55),
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .BorderStyle = BorderStyle.None,
            .ColumnHeadersDefaultCellStyle = New DataGridViewCellStyle() With {
                .BackColor = Color.FromArgb(40, 50, 65),
                .ForeColor = Color.White,
                .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
            },
            .DefaultCellStyle = New DataGridViewCellStyle() With {
                .BackColor = Color.FromArgb(30, 40, 55),
                .ForeColor = Color.White,
                .SelectionBackColor = Color.FromArgb(50, 70, 100),
                .SelectionForeColor = Color.White
            },
            .EnableHeadersVisualStyles = False,
            .RowHeadersVisible = False
        }
    End Function

End Class
