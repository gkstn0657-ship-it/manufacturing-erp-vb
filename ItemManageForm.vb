Imports System.Drawing
Imports System.Windows.Forms
Imports System.Data

''' <summary>
''' 부품(품목) 관리 폼
''' 품목 등록, BOM 연결, 삭제 기능
''' 관리자 / 생산관리자 전용
''' </summary>
Public Class ItemManageForm
    Inherits Form

    Private ReadOnly _stockRepo As New StockRepository()
    Private ReadOnly _userRepo As New UserRepository()

    ' ── 탭 ──
    Private tabControl As TabControl
    Private tabAdd As TabPage
    Private tabBom As TabPage

    ' ── 품목 등록 탭 컨트롤 ──
    Private txtItemCd As TextBox
    Private txtItemNm As TextBox
    Private cbItemType As ComboBox
    Private nudInitQty As NumericUpDown
    Private nudSafetyQty As NumericUpDown
    Private btnRegister As Button
    Private lblResult As Label

    ' ── 기존 품목 목록 ──
    Private dgvItems As DataGridView
    Private btnDeleteItem As Button
    Private btnRefreshList As Button

    ' ── BOM 연결 탭 컨트롤 ──
    Private cbParent As ComboBox
    Private cbChild As ComboBox
    Private nudBomQty As NumericUpDown
    Private btnAddBom As Button
    Private btnDeleteBom As Button
    Private dgvBom As DataGridView
    Private lblBomResult As Label

    ''' <summary>등록 완료 후 메인 폼 새로고침 콜백</summary>
    Public Property OnDataChanged As Action

    Public Sub New()
        BuildUI()
    End Sub

    Private Sub BuildUI()
        Me.Text = "부품(품목) 관리"
        Me.Size = New Size(820, 680)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.FromArgb(24, 34, 50)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False

        tabControl = New TabControl() With {.Dock = DockStyle.Fill}
        Me.Controls.Add(tabControl)

        ' ── 탭 1: 품목 등록 ──
        tabAdd = New TabPage("품목 등록 / 삭제") With {.BackColor = Color.FromArgb(24, 34, 50)}
        tabControl.TabPages.Add(tabAdd)
        BuildAddTab()

        ' ── 탭 2: BOM 연결 ──
        tabBom = New TabPage("BOM 구성 관리") With {.BackColor = Color.FromArgb(24, 34, 50)}
        tabControl.TabPages.Add(tabBom)
        BuildBomTab()
    End Sub

    ' ══════════════════════════════════════
    ' 탭 1: 품목 등록
    ' ══════════════════════════════════════
    Private Sub BuildAddTab()
        Dim lblTitle As New Label() With {
            .Text = "[신규 부품 등록]",
            .Font = New Font("맑은 고딕", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(33, 150, 243),
            .Location = New Point(20, 15), .Size = New Size(300, 25)
        }

        ' 품목 코드
        Dim lblCd As New Label() With {.Text = "품목 코드", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(20, 50), .Size = New Size(100, 20)}
        txtItemCd = New TextBox() With {.Location = New Point(130, 48), .Size = New Size(200, 26), .Font = New Font("맑은 고딕", 10), .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White, .BorderStyle = BorderStyle.FixedSingle}

        ' 품목 이름
        Dim lblNm As New Label() With {.Text = "품목명", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(350, 50), .Size = New Size(80, 20)}
        txtItemNm = New TextBox() With {.Location = New Point(430, 48), .Size = New Size(340, 26), .Font = New Font("맑은 고딕", 10), .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White, .BorderStyle = BorderStyle.FixedSingle}

        ' 품목 타입
        Dim lblType As New Label() With {.Text = "품목 타입", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(20, 85), .Size = New Size(100, 20)}
        cbItemType = New ComboBox() With {.Location = New Point(130, 83), .Size = New Size(200, 26), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 10)}
        cbItemType.Items.AddRange(New String() {"RM - 원자재", "SFG - 반제품", "FG - 완제품"})
        cbItemType.SelectedIndex = 0

        ' 초기 재고
        Dim lblQty As New Label() With {.Text = "초기 재고", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(350, 85), .Size = New Size(80, 20)}
        nudInitQty = New NumericUpDown() With {.Location = New Point(430, 83), .Size = New Size(120, 26), .Font = New Font("맑은 고딕", 10), .Maximum = 999999, .DecimalPlaces = 1, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        ' 안전 재고
        Dim lblSafe As New Label() With {.Text = "안전 재고", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(570, 85), .Size = New Size(80, 20)}
        nudSafetyQty = New NumericUpDown() With {.Location = New Point(650, 83), .Size = New Size(120, 26), .Font = New Font("맑은 고딕", 10), .Maximum = 999999, .DecimalPlaces = 1, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        ' 등록 버튼
        btnRegister = New Button() With {
            .Text = "부품 등록",
            .Location = New Point(20, 120), .Size = New Size(140, 36),
            .BackColor = Color.FromArgb(76, 175, 80), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold)
        }
        AddHandler btnRegister.Click, AddressOf BtnRegister_Click

        ' 삭제 버튼
        btnDeleteItem = New Button() With {
            .Text = "선택 품목 삭제",
            .Location = New Point(170, 120), .Size = New Size(140, 36),
            .BackColor = Color.FromArgb(244, 67, 54), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold)
        }
        AddHandler btnDeleteItem.Click, AddressOf BtnDeleteItem_Click

        ' 새로고침 버튼
        btnRefreshList = New Button() With {
            .Text = "목록 새로고침",
            .Location = New Point(320, 120), .Size = New Size(130, 36),
            .BackColor = Color.FromArgb(33, 150, 243), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnRefreshList.Click, Sub() LoadItemList()

        ' 결과 라벨
        lblResult = New Label() With {
            .Text = "", .ForeColor = Color.LimeGreen,
            .Font = New Font("맑은 고딕", 9), .Location = New Point(460, 128), .Size = New Size(310, 20)
        }

        ' 품목 목록 그리드
        Dim lblListTitle As New Label() With {
            .Text = "[등록된 전체 품목 목록]",
            .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold),
            .ForeColor = Color.FromArgb(255, 183, 77),
            .Location = New Point(20, 165), .Size = New Size(300, 20)
        }
        dgvItems = CreateDarkGrid(New Point(20, 190), New Size(760, 420))

        tabAdd.Controls.AddRange(New Control() {
            lblTitle, lblCd, txtItemCd, lblNm, txtItemNm,
            lblType, cbItemType, lblQty, nudInitQty, lblSafe, nudSafetyQty,
            btnRegister, btnDeleteItem, btnRefreshList, lblResult,
            lblListTitle, dgvItems
        })

        LoadItemList()
    End Sub

    ' ══════════════════════════════════════
    ' 탭 2: BOM 구성 관리
    ' ══════════════════════════════════════
    Private Sub BuildBomTab()
        Dim lblTitle As New Label() With {
            .Text = "[BOM 구성 추가/삭제] 부모 품목에 자식 부품을 연결합니다.",
            .Font = New Font("맑은 고딕", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(33, 150, 243),
            .Location = New Point(20, 15), .Size = New Size(600, 25)
        }

        ' 부모 품목
        Dim lblParent As New Label() With {.Text = "부모 품목", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(20, 50), .Size = New Size(80, 20)}
        cbParent = New ComboBox() With {.Location = New Point(110, 48), .Size = New Size(320, 26), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}
        AddHandler cbParent.SelectedIndexChanged, Sub() LoadBomChildren()

        ' 자식 품목
        Dim lblChild As New Label() With {.Text = "자식 부품", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(20, 85), .Size = New Size(80, 20)}
        cbChild = New ComboBox() With {.Location = New Point(110, 83), .Size = New Size(320, 26), .DropDownStyle = ComboBoxStyle.DropDownList, .Font = New Font("맑은 고딕", 9)}

        ' 소요량
        Dim lblQty As New Label() With {.Text = "소요량", .ForeColor = Color.LightGray, .Font = New Font("맑은 고딕", 9, FontStyle.Bold), .Location = New Point(450, 85), .Size = New Size(60, 20)}
        nudBomQty = New NumericUpDown() With {.Location = New Point(520, 83), .Size = New Size(100, 26), .Font = New Font("맑은 고딕", 10), .Maximum = 99999, .Minimum = 0.1D, .Value = 1, .DecimalPlaces = 2, .Increment = 0.5D, .BackColor = Color.FromArgb(35, 45, 60), .ForeColor = Color.White}

        ' BOM 추가 버튼
        btnAddBom = New Button() With {
            .Text = "BOM 추가",
            .Location = New Point(450, 48), .Size = New Size(100, 30),
            .BackColor = Color.FromArgb(76, 175, 80), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnAddBom.Click, AddressOf BtnAddBom_Click

        ' BOM 삭제 버튼
        btnDeleteBom = New Button() With {
            .Text = "선택 BOM 삭제",
            .Location = New Point(560, 48), .Size = New Size(120, 30),
            .BackColor = Color.FromArgb(244, 67, 54), .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat, .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
        }
        AddHandler btnDeleteBom.Click, AddressOf BtnDeleteBom_Click

        ' BOM 결과 라벨
        lblBomResult = New Label() With {
            .Text = "", .ForeColor = Color.LimeGreen,
            .Font = New Font("맑은 고딕", 9), .Location = New Point(640, 87), .Size = New Size(140, 20)
        }

        ' BOM 자식 목록 그리드
        Dim lblBomList As New Label() With {
            .Text = "[선택된 부모 품목의 BOM 구성]",
            .Font = New Font("맑은 고딕", 9.5, FontStyle.Bold),
            .ForeColor = Color.FromArgb(255, 183, 77),
            .Location = New Point(20, 120), .Size = New Size(400, 20)
        }
        dgvBom = CreateDarkGrid(New Point(20, 145), New Size(760, 470))

        tabBom.Controls.AddRange(New Control() {
            lblTitle, lblParent, cbParent, lblChild, cbChild,
            lblQty, nudBomQty, btnAddBom, btnDeleteBom, lblBomResult,
            lblBomList, dgvBom
        })

        LoadComboItems()
    End Sub

    ' ══════════════════════════════════════
    ' 이벤트 핸들러
    ' ══════════════════════════════════════

    ''' <summary>부품 등록</summary>
    Private Sub BtnRegister_Click(sender As Object, e As EventArgs)
        lblResult.ForeColor = Color.OrangeRed

        ' 입력 검증
        Dim itemCd = txtItemCd.Text.Trim().ToUpper()
        Dim itemNm = txtItemNm.Text.Trim()

        If String.IsNullOrEmpty(itemCd) Then
            lblResult.Text = "품목 코드를 입력하세요."
            Return
        End If
        If String.IsNullOrEmpty(itemNm) Then
            lblResult.Text = "품목명을 입력하세요."
            Return
        End If
        If cbItemType.SelectedIndex < 0 Then
            lblResult.Text = "품목 타입을 선택하세요."
            Return
        End If

        ' 품목 타입 코드 추출
        Dim itemType As String = cbItemType.SelectedItem.ToString().Split(" "c)(0)

        ' 중복 체크
        If _stockRepo.ItemExists(itemCd) Then
            lblResult.Text = $"이미 존재하는 품목 코드: {itemCd}"
            Return
        End If

        Try
            _stockRepo.RegisterNewItem(itemCd, itemNm, itemType, CDbl(nudInitQty.Value), CDbl(nudSafetyQty.Value))

            lblResult.ForeColor = Color.LimeGreen
            lblResult.Text = $"{itemCd} 등록 완료!"

            ' 입력 필드 초기화
            txtItemCd.Clear()
            txtItemNm.Clear()
            nudInitQty.Value = 0
            nudSafetyQty.Value = 0
            txtItemCd.Focus()

            ' 목록 새로고침
            LoadItemList()
            LoadComboItems()

            ' 메인 폼 데이터 갱신 콜백
            OnDataChanged?.Invoke()

        Catch ex As Exception
            lblResult.Text = $"등록 오류: {ex.Message}"
        End Try
    End Sub

    ''' <summary>품목 삭제</summary>
    Private Sub BtnDeleteItem_Click(sender As Object, e As EventArgs)
        If dgvItems.CurrentRow Is Nothing Then
            lblResult.ForeColor = Color.OrangeRed
            lblResult.Text = "삭제할 품목을 선택하세요."
            Return
        End If

        Dim itemCd = dgvItems.CurrentRow.Cells("품목코드").Value.ToString()
        Dim itemNm = dgvItems.CurrentRow.Cells("품목명").Value.ToString()

        If MessageBox.Show($"'{itemCd} ({itemNm})' 품목을 삭제하시겠습니까?" & Environment.NewLine &
                           "관련 BOM, 재고, 이력이 모두 삭제됩니다.",
                           "품목 삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            Try
                _stockRepo.DeleteItem(itemCd)

                lblResult.ForeColor = Color.LimeGreen
                lblResult.Text = $"{itemCd} 삭제 완료"

                LoadItemList()
                LoadComboItems()
                OnDataChanged?.Invoke()
            Catch ex As Exception
                lblResult.ForeColor = Color.OrangeRed
                lblResult.Text = $"삭제 오류: {ex.Message}"
            End Try
        End If
    End Sub

    ''' <summary>BOM 추가</summary>
    Private Sub BtnAddBom_Click(sender As Object, e As EventArgs)
        lblBomResult.ForeColor = Color.OrangeRed

        If cbParent.SelectedIndex < 0 Then
            lblBomResult.Text = "부모 품목 선택"
            Return
        End If
        If cbChild.SelectedIndex < 0 Then
            lblBomResult.Text = "자식 품목 선택"
            Return
        End If

        Dim parentCd = cbParent.SelectedItem.ToString().Split(" "c)(0)
        Dim childCd = cbChild.SelectedItem.ToString().Split(" "c)(0)

        If parentCd = childCd Then
            lblBomResult.Text = "부모=자식 불가"
            Return
        End If

        Try
            _stockRepo.RegisterBomLink(parentCd, childCd, CDbl(nudBomQty.Value))

            lblBomResult.ForeColor = Color.LimeGreen
            lblBomResult.Text = "BOM 추가 완료"

            LoadBomChildren()
            OnDataChanged?.Invoke()
        Catch ex As Exception
            lblBomResult.Text = $"오류: {ex.Message}"
        End Try
    End Sub

    ''' <summary>BOM 삭제</summary>
    Private Sub BtnDeleteBom_Click(sender As Object, e As EventArgs)
        If cbParent.SelectedIndex < 0 OrElse dgvBom.CurrentRow Is Nothing Then
            lblBomResult.ForeColor = Color.OrangeRed
            lblBomResult.Text = "삭제할 BOM을 선택하세요."
            Return
        End If

        Dim parentCd = cbParent.SelectedItem.ToString().Split(" "c)(0)
        Dim childCd = dgvBom.CurrentRow.Cells("자식품목코드").Value.ToString()

        If MessageBox.Show($"BOM 관계 삭제: {parentCd} -> {childCd}", "BOM 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            Try
                _stockRepo.DeleteBomLink(parentCd, childCd)

                lblBomResult.ForeColor = Color.LimeGreen
                lblBomResult.Text = "BOM 삭제 완료"

                LoadBomChildren()
                OnDataChanged?.Invoke()
            Catch ex As Exception
                lblBomResult.ForeColor = Color.OrangeRed
                lblBomResult.Text = $"오류: {ex.Message}"
            End Try
        End If
    End Sub

    ' ══════════════════════════════════════
    ' 데이터 로드
    ' ══════════════════════════════════════

    Private Sub LoadItemList()
        Try
            dgvItems.DataSource = _stockRepo.GetDashboardStock()
            dgvItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        Catch ex As Exception
            ' ignore
        End Try
    End Sub

    Private Sub LoadComboItems()
        Try
            Dim dt = _userRepo.GetAllItemList()
            cbParent.Items.Clear()
            cbChild.Items.Clear()

            For Each row As DataRow In dt.Rows
                Dim display = $"{row("ITEM_CD")} ({row("ITEM_NM")}) [{row("ITEM_TYPE")}]"
                cbParent.Items.Add(display)
                cbChild.Items.Add(display)
            Next
        Catch ex As Exception
            ' ignore
        End Try
    End Sub

    Private Sub LoadBomChildren()
        If cbParent.SelectedIndex < 0 Then Return
        Try
            Dim parentCd = cbParent.SelectedItem.ToString().Split(" "c)(0)
            dgvBom.DataSource = _stockRepo.GetBomChildrenTable(parentCd)
            dgvBom.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        Catch ex As Exception
            ' ignore
        End Try
    End Sub

    ' ══════════════════════════════════════
    ' 유틸리티
    ' ══════════════════════════════════════
    Private Function CreateDarkGrid(loc As Point, sz As Size) As DataGridView
        Return New DataGridView() With {
            .Location = loc, .Size = sz,
            .ReadOnly = True, .AllowUserToAddRows = False,
            .BackgroundColor = Color.FromArgb(30, 40, 55),
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .BorderStyle = BorderStyle.None,
            .ColumnHeadersDefaultCellStyle = New DataGridViewCellStyle() With {
                .BackColor = Color.FromArgb(40, 50, 65), .ForeColor = Color.White,
                .Font = New Font("맑은 고딕", 9, FontStyle.Bold)
            },
            .DefaultCellStyle = New DataGridViewCellStyle() With {
                .BackColor = Color.FromArgb(30, 40, 55), .ForeColor = Color.White,
                .SelectionBackColor = Color.FromArgb(50, 70, 100), .SelectionForeColor = Color.White
            },
            .EnableHeadersVisualStyles = False, .RowHeadersVisible = False
        }
    End Function
End Class
