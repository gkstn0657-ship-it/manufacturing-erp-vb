Imports System
Imports System.Linq

''' <summary>
''' 바코드/LOT 관리 서비스
''' 자동차 제조업 표준 LOT 번호 및 바코드 시리얼 체계 생성
''' </summary>
Public Class BarcodeService

    Private Shared _seqCounter As Integer = 0
    Private Shared ReadOnly _lock As New Object()

    ''' <summary>
    ''' 생산 LOT 번호 생성
    ''' 형식: YYYYMMDD-{라인코드}-{4자리 시퀀스}
    ''' 예: 20260527-LINE-WELD-01-0001
    ''' </summary>
    Public Shared Function GenerateLotNo(lineCd As String) As String
        SyncLock _lock
            _seqCounter += 1
            Return $"{DateTime.Now:yyyyMMdd}-{lineCd}-{_seqCounter:D4}"
        End SyncLock
    End Function

    ''' <summary>
    ''' 서열 지시용 LOT 번호 생성
    ''' 형식: YYYYMMDD-SEQ-{4자리 시퀀스}
    ''' </summary>
    Public Shared Function GenerateSequenceLotNo(seqNo As Integer) As String
        Return $"{DateTime.Now:yyyyMMdd}-SEQ-{seqNo:D4}"
    End Function

    ''' <summary>
    ''' 바코드 시리얼 넘버 생성 (GS1-128 호환 형식)
    ''' 형식: KVC{공정코드}{YYMMDD}{6자리 랜덤}
    ''' 예: KVCWELD260527012345
    ''' </summary>
    Public Shared Function GenerateBarcodeSn(processStep As ProcessStep) As String
        Dim processCode As String
        Select Case processStep
            Case ProcessStep.Press : processCode = "PRSS"
            Case ProcessStep.Welding : processCode = "WELD"
            Case ProcessStep.Painting : processCode = "PANT"
            Case ProcessStep.Assembly : processCode = "ASSY"
            Case ProcessStep.Inspection : processCode = "INSP"
            Case Else : processCode = "UNKN"
        End Select

        Dim rand As New Random()
        Return $"KVC{processCode}{DateTime.Now:yyMMdd}{rand.Next(100000, 999999)}"
    End Function

    ''' <summary>
    ''' 바코드 유효성 검증
    ''' KVC 접두사 + 4자리 공정코드 + 6자리 날짜 + 6자리 시리얼
    ''' </summary>
    Public Shared Function ValidateBarcode(barcode As String) As Boolean
        If String.IsNullOrWhiteSpace(barcode) Then Return False
        If barcode.Length <> 19 Then Return False
        If Not barcode.StartsWith("KVC") Then Return False

        Dim processCode As String = barcode.Substring(3, 4)
        Dim validCodes As String() = {"PRSS", "WELD", "PANT", "ASSY", "INSP"}
        If Not validCodes.Contains(processCode) Then Return False

        Return True
    End Function

    ''' <summary>바코드에서 공정 코드 추출</summary>
    Public Shared Function ExtractProcessFromBarcode(barcode As String) As ProcessStep
        If Not ValidateBarcode(barcode) Then
            Throw New BarcodeException(barcode, $"유효하지 않은 바코드 형식: {barcode}")
        End If

        Dim processCode As String = barcode.Substring(3, 4)
        Select Case processCode
            Case "PRSS" : Return ProcessStep.Press
            Case "WELD" : Return ProcessStep.Welding
            Case "PANT" : Return ProcessStep.Painting
            Case "ASSY" : Return ProcessStep.Assembly
            Case "INSP" : Return ProcessStep.Inspection
            Case Else
                Throw New BarcodeException(barcode, $"알 수 없는 공정 코드: {processCode}")
        End Select
    End Function

    ''' <summary>시퀀스 카운터 초기화</summary>
    Public Shared Sub ResetSequence()
        SyncLock _lock
            _seqCounter = 0
        End SyncLock
    End Sub
End Class
