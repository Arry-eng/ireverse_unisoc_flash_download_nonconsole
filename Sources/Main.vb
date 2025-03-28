Imports System.IO
Imports System.Runtime.InteropServices
Imports UniFlash.USBFastConnect

Public Class Main

#Region "Disable Sleep"
	<DllImport("kernel32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
	Public Shared Function SetThreadExecutionState(ByVal esFlags As EXECUTION_STATE) As EXECUTION_STATE
	End Function

	Public Enum EXECUTION_STATE As UInteger
		ES_SYSTEM_REQUIRED = &H1
		ES_DISPLAY_REQUIRED = &H2
		ES_CONTINUOUS = &H80000000UI
	End Enum

	Public Shared Sub PreventSleep()
		SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS Or EXECUTION_STATE.ES_SYSTEM_REQUIRED Or EXECUTION_STATE.ES_DISPLAY_REQUIRED)
	End Sub

	Public Shared Sub AllowSleep()
		SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS)
	End Sub
#End Region
#Const DEBUG = True
	Friend Shared SharedUI As Main

	Private flashTimer As Timer
	Private flashDuration As Integer = 3000 ' Duration in milliseconds
	Private flashElapsed As Integer = 0
	Private flashControl As Control
	Private flashHandler As Action(Of Control)

	Public Sub New()
		InitializeComponent()
		SharedUI = Me
		getcomInfo()
		PreventSleep()
		AddHandler UnisocWorker.DoWork, AddressOf uni_worker.UnisocWorker_DoWork
		AddHandler UnisocWorker.RunWorkerCompleted, AddressOf UnisocWorker_RunWorkerCompleted
		AddHandler ReceiverDataWorker.DoWork, AddressOf ReceiverDataWorker_DoWork
		AddHandler ReceiverDataWorker.RunWorkerCompleted, AddressOf ReceiverDataWorker_RunWorkerCompleted
		Console.WriteLine()

	End Sub

	' Code for the flash of an error message at bottom of the screen in the LabelErrorMsg control
#Region "Flash Error Message" '' Flash error message in the LabelErrorMsg control at the bottom of the main form
	''' <summary>
	''' Flashes the text of a control by toggling its color Red and Blue
	''' </summary>
	''' <param name="flashInterval">The interval in milliseconds to flash the text</param>
	''' <param name="handler">The function to handle the control's text color</param>
	''' <param name="control">The control to flash</param>
	''' <remarks>Requires a Timer object to be created and initialized</remarks>
	''' <exception cref="ArgumentException">Thrown when the flash interval is less than or equal to 0</exception>
	''' <exception cref="ArgumentNullException">Thrown when the handler function or control is Nothing</exception>
	''' <exception cref="InvalidOperationException">Thrown when the Timer object is not initialized</exception>
	''' <exception cref="Exception">Thrown when an unknown error occurs</exception>
	Public Sub FlashText(flashInterval As Integer, handler As Action(Of Control), control As Control)
		'' Check if the flash interval is greater than 0    
		Debug.Assert(flashInterval > 0, "ERROR: Flash interval must be greater than 0")
		'' Check if the handler function is Nothing
		Debug.Assert(handler IsNot Nothing, "ERROR: Handler function is Nothing")
		'' Check if the control is Nothing
		Debug.Assert(control IsNot Nothing, "ERROR: Control object is Nothing")

		' Set the handler function and control
		flashHandler = handler
		flashControl = control

		If flashTimer IsNot Nothing Then
			' Initialize the Timer if it hasn't been created yet
			flashTimer = New Timer()
			AddHandler flashTimer.Tick, AddressOf FlashTimer_Tick
			flashTimer.Interval = flashInterval
		End If
		'' flashTimer should not be Nothing at this point
		Debug.Assert(flashTimer IsNot Nothing, "ERROR: Timer object \'flashTime\' Not initialized")
		' Set the timer interval
		flashTimer.Interval = flashInterval

		' Reset the elapsed time and start the timer
		flashElapsed = 0
		flashTimer.Start()
	End Sub
	''' <summary>
	''' Flashes an error message in the LabelErrorMsg control
	''' </summary>
	''' <param name="errorMessage">The error message to display</param>
	''' <remarks>Requires the LabelErrorMsg control to be created and initialized</remarks>
	''' <exception cref="ArgumentNullException">Thrown when the error message is empty or Nothing</exception>
	''' <exception cref="InvalidOperationException">Thrown when the LabelErrorMsg control is not initialized</exception>
	''' <exception cref="Exception">Thrown when an unknown error occurs</exception>
	Private Sub FlashErrorMsg(errorMessage As String)
		'' Check if the LabelErrorMsg control is Nothing
		Debug.Assert(UniFlash.Main.SharedUI.LabelErrorMsg IsNot Nothing, "ERROR: LabelErrorMsg control is Nothing")
		''  Check if the error message is empty
		Debug.Assert(Not String.IsNullOrEmpty(errorMessage), "ERROR: Error message is empty")

		'' Set the error message in the LabelErrorMsg control
		LabelErrorMsg.Text = errorMessage
		Dim handler As Action(Of Control) = AddressOf ToggleControlColor
		'' Check if the handler is Nothing
		Debug.Assert(handler IsNot Nothing, "ERROR: ToggleControlColor is Nothing")

		'' Flash the error message in the LabelErrorMsg control
		FlashText(500, handler, LabelErrorMsg)
	End Sub

	Private Sub ToggleControlColor(control As Control)
		' Toggle the control's text color
		If control.ForeColor = Color.Red Then
			control.ForeColor = Color.Blue
		Else
			control.ForeColor = Color.Red
		End If
	End Sub
	Private Sub FlashTimer_Tick(sender As Object, e As EventArgs)
		' Call the handler function to update the control
		flashHandler(flashControl)

		' Increment the elapsed time
		flashElapsed += flashTimer.Interval

		' Stop the timer after the duration has elapsed
		If flashElapsed >= flashDuration Then
			flashTimer.Stop()
			flashControl.ResetText() ' Reset to default color
		End If
	End Sub
#End Region

	Private Sub Main_Closing() Handles MyBase.FormClosing
		''Stop the timer if it is running and dispose it
		If flashTimer IsNot Nothing Then
			flashTimer.Stop()
			flashTimer.Dispose()
		End If

		AllowSleep()
	End Sub

	Private Sub Logs_TextChanged(sender As Object, e As EventArgs) Handles Logs.TextChanged
		Logs.Invoke(Sub()
						Logs.SelectionStart = Logs.SelectionStart
						Logs.ScrollToCaret()
					End Sub)
	End Sub

	Private Sub ComboPort_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboPort.SelectedIndexChanged
		If ComboPort.Text = "" Then
			CkFDLLoaded.Checked = False
		End If
	End Sub
	Private Sub CkFDLLoaded_CheckedChanged(sender As Object, e As EventArgs) Handles CkFDLLoaded.CheckedChanged
		If CkFDLLoaded.Checked Then
			BtnStart.Text = "Flash"
			WorkerMethod = "Flash"
		Else
			BtnStart.Text = "Download"
			WorkerMethod = "Download"
		End If
	End Sub
	Private Sub CkPartition_CheckedChanged(sender As Object, e As EventArgs) Handles CkPartition.CheckedChanged
		If CkPartition.CheckState = CheckState.Checked Then
			For Each item As DataGridViewRow In DataView.Rows
				For i As Integer = 0 To item.Cells.Count - 1
					item.Cells(0).Value = True
				Next
			Next
		Else
			For Each item As DataGridViewRow In DataView.Rows
				For i As Integer = 0 To item.Cells.Count - 1
					item.Cells(0).Value = False
				Next
			Next
		End If
	End Sub
	Private Sub Rdlibusb_CheckedChanged(sender As Object, e As EventArgs) Handles Rdlibusb.CheckedChanged
		If Rdlibusb.Checked Then
			RdDiagChannel.Checked = False
			RdSerialPort.Checked = False
			USBMethod = "libusb-win32"
		End If
	End Sub
	Private Sub RdSerialPort_CheckedChanged(sender As Object, e As EventArgs) Handles RdSerialPort.CheckedChanged
		If RdSerialPort.Checked Then
			RdDiagChannel.Checked = False
			Rdlibusb.Checked = False
			USBMethod = "Serial Port"
		End If
	End Sub
	Private Sub RdDiagChannel_CheckedChanged(sender As Object, e As EventArgs) Handles RdDiagChannel.CheckedChanged
		If RdDiagChannel.Checked Then
			Rdlibusb.Checked = False
			RdSerialPort.Checked = False
			USBMethod = "Diag Channel"
		End If
	End Sub

	Private Sub BtnStart_Click(sender As Object, e As EventArgs) Handles BtnStart.Click
		If Not UnisocWorker.IsBusy Then
			If Not CkFDLLoaded.Checked Then
				RtbClear()
				ProcessBar1(0)
				ProcessBar2(0)
				WorkerMethod = "Download"
				UnisocWorker.RunWorkerAsync()
				UnisocWorker.Dispose()
			Else

				Dim flag As Boolean

				For Each item As DataGridViewRow In DataView.Rows
					If item.Cells(0).Value = True Then
						flag = True
					End If
				Next

				If flag Then

					RtbClear()
					ProcessBar1(0)
					ProcessBar2(0)
					WorkerMethod = "Flash"

					StringXML = ""

					StringXML = String.Concat(StringXML, "<?xml version=""1.0"" ?>" & vbCrLf & "")
					StringXML = String.Concat(StringXML, "<Partitions>" & vbCrLf & "")


					totalchecked = 0
					For Each item As DataGridViewRow In DataView.Rows
						If item.Cells(DataView.Columns(0).Index).Value = True Then
							totalchecked += 1

							StringXML = String.Concat(StringXML, String.Format("<Partition id=""{0}"" startsector=""{1}"" endsector=""{2}"" size=""{3}"" locations=""{4}""/>", New Object() {
											item.Cells(DataView.Columns(2).Index).Value.ToString(),                   'id   = partition
											item.Cells(DataView.Columns(3).Index).Value.ToString(),                   'id   = startsector
											item.Cells(DataView.Columns(4).Index).Value.ToString(),                   'id   = endsector
											item.Cells(DataView.Columns(5).Index).Value.ToString().Replace("B", ""),  'id   = partition size
											item.Cells(DataView.Columns(6).Index).Value.ToString()                    'size = file locations
											}),
											"" & vbCrLf & "")

						End If
					Next

					StringXML = String.Concat(StringXML, "</Partitions>")
				End If

				UnisocWorker.RunWorkerAsync()
				UnisocWorker.Dispose()
			End If
		End If
	End Sub

	Private Sub BtnReadPartition_Click(sender As Object, e As EventArgs) Handles BtnReadPartition.Click
		If Not UnisocWorker.IsBusy Then
			Dim flag As Boolean

			For Each item As DataGridViewRow In DataView.Rows
				If item.Cells(0).Value = True Then
					flag = True
				End If
			Next

			If flag Then
				Dim folderBrowserDialog As New FolderBrowserDialog() With
											{
											.ShowNewFolderButton = True
											}

				If folderBrowserDialog.ShowDialog() = DialogResult.OK Then
					RtbClear()
					ProcessBar1(0)
					ProcessBar2(0)
					WorkerMethod = "Read Partition"
					foldersave = folderBrowserDialog.SelectedPath
					StringXML = GenerateStringXML()
				End If
				UnisocWorker.RunWorkerAsync()
				UnisocWorker.Dispose()
			End If

		End If
	End Sub
	Private Sub BtnErase_Click(sender As Object, e As EventArgs) Handles BtnErase.Click
		'set_chksum_type("add")
		'send_read("a", "20M")
		'If Not UnisocWorker.IsBusy Then
		'    RtbClear()
		'    ProcessBar1(0)
		'    ProcessBar2(0)
		'    WorkerMethod = "Parse"
		'    UnisocWorker.RunWorkerAsync()
		'    UnisocWorker.Dispose()
		'End If

		If Not UnisocWorker.IsBusy Then
			Dim flag As Boolean

			For Each item As DataGridViewRow In DataView.Rows
				If item.Cells(0).Value = True Then
					flag = True
				End If
			Next

			If flag Then
				RtbClear()
				ProcessBar1(0)
				ProcessBar2(0)
				WorkerMethod = "Erase Partition"
				StringXML = GenerateStringXML()
			End If

			UnisocWorker.RunWorkerAsync()
			UnisocWorker.Dispose()
		End If

	End Sub
	Private Function GenerateStringXML() As String 'returns StringXML As String

		Dim PartitionsXML As String = ""

		PartitionsXML = String.Concat(PartitionsXML, "<?xml version=""1.0"" ?>" & vbCrLf & "")
		PartitionsXML = String.Concat(PartitionsXML, "<Partitions>" & vbCrLf & "")


		totalchecked = 0
		For Each item As DataGridViewRow In DataView.Rows
			If item.Cells(DataView.Columns(0).Index).Value = True Then
				totalchecked += 1

				PartitionsXML = String.Concat(PartitionsXML, String.Format("<Partition id=""{0}"" size=""{1}""/>", New Object() {
								item.Cells(DataView.Columns(2).Index).Value.ToString(),                   'id   = partition
								item.Cells(DataView.Columns(5).Index).Value.ToString().Replace("B", "")  'size = partition size
								}),
								"" & vbCrLf & "")

			End If
		Next

		PartitionsXML = String.Concat(PartitionsXML, "</Partitions>")
		Return PartitionsXML
	End Function
	Private Sub BtnPACFirmware_Click(sender As Object, e As EventArgs) Handles BtnPACFirmware.Click
		If Not UnisocWorker.IsBusy Then
			Dim filename As String
			filename = GetFileNameFromBrowseDialog("Select PAC Firmware", "PAC Firmware |*.pac* ")
			If (filename.Length) Then
				DGVClear()
				RtbClear()
				ProcessBar1(0)
				WorkerMethod = "PAC Firmware"
				TxtPacFirmware.Text = filename
				Firmware = filename
				UnisocWorker.RunWorkerAsync()
				UnisocWorker.Dispose()
			End If

		End If
	End Sub

	Private Sub BtnSavePACPartitionsTable_Click(sender As Object, e As EventArgs) Handles BtnSavePACPartitionsTable.Click
		If Not UnisocWorker.IsBusy AndAlso TextBoxSaveToPartitionsTableFile.Text.Length > 5 Then
			'Copy the file name from the TextBox - User is allowed to edit it as well as select a file through brose dialog
			uni_worker.PACPartitionsTableXMLFile = TextBoxSaveToPartitionsTableFile.Text
			''DGVClear()
			''RtbClear()
			ProcessBar1(0)
			WorkerMethod = "Save PACPartitionsTable"
			uni_worker.StringXML = GenerateStringXML()
			UnisocWorker.RunWorkerAsync()
			UnisocWorker.Dispose()
		End If
	End Sub

	'Opens a Dialog box for the user to browse and select a file. Return filename with full path on selection or empty string if canceled.
	Private Function GetFileNameFromBrowseDialog(Optional title As String = "Select path to the file", Optional filter As String = "All Files (*.*)|*.*") As String
		Dim openFileDialog As New OpenFileDialog() With
				{
				.Title = "",
				.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Recent),
				.FileName = "*.*",
				.Filter = filter,
				.FilterIndex = 2,
				.RestoreDirectory = True
				}
		If openFileDialog.ShowDialog() = DialogResult.OK Then
			Return openFileDialog.FileName
		End If
		Return "" '"OK" not pressed. Return empty string. 
	End Function

	Private Sub BtnSaveToPartitionsTableFile_Click(sender As Object, e As EventArgs) Handles BtnSaveToPartitionsTableFile.Click
		If Not UnisocWorker.IsBusy Then
			Dim filename As String
			filename = GetFileNameFromBrowseDialog("Select path to XML file to save PACPartitionsTable", "XML Files (*.xml)|*.xml")
			If (filename.Length) Then
				TextBoxSaveToPartitionsTableFile.Text = filename
				PACPartitionsTableXMLFile = filename
			End If
		End If
	End Sub

	Private Sub BtnSavePhonePartitionsTable_Click(sender As Object, e As EventArgs) Handles BtnSavePhonePartitionsTable.Click
		If Not UnisocWorker.IsBusy Then
			Dim filename As String
			filename = GetFileNameFromBrowseDialog("Select path to XML file to save PACPartitionsTable", "XML Files (*.xml)|*.xml")
			If (filename.Length) Then
				TextBoxSavePhonePartitionsTable.Text = filename
				PhonePartitionsTableXMLFile = filename
			End If
		End If
	End Sub

	Private Sub BtnBrowsePhonePartitionsTable_Click(sender As Object, e As EventArgs) Handles BtnBrowsePhonePartitionsTable.Click
		If Not UnisocWorker.IsBusy Then
			Dim filename As String
			filename = GetFileNameFromBrowseDialog("Select path to XML file to save PACPartitionsTable", "XML Files (*.xml)|*.xml")
			If (filename.Length) Then
				TextBoxSavePhonePartitionsTable.Text = filename
				PhonePartitionsTableXMLFile = filename
			End If
		End If
	End Sub

	Private Sub TextBoxSaveToPartitionsTableFile_TextChanged(sender As Object, e As EventArgs) Handles TextBoxSaveToPartitionsTableFile.TextChanged
		Dim filename As String = TextBoxSaveToPartitionsTableFile.Text.Trim()
		filename = GetValidFilePath(filename)
		If Not String.IsNullOrEmpty(filename) Then
			PACPartitionsTableXMLFile = filename
		End If
		TextBoxSaveToPartitionsTableFile.Text = PACPartitionsTableXMLFile
	End Sub

	Private Sub TextBoxSavePhonePartitionsTable_TextChanged(sender As Object, e As EventArgs) Handles TextBoxSavePhonePartitionsTable.TextChanged
		Dim filename As String = TextBoxSavePhonePartitionsTable.Text.Trim()
		filename = GetValidFilePath(filename)
		If Not String.IsNullOrEmpty(filename) Then
			PhonePartitionsTableXMLFile = filename
		End If
		TextBoxSavePhonePartitionsTable.Text = PhonePartitionsTableXMLFile
	End Sub
#Region "CheckAndCorrectFilePath"
	Protected Friend Function GetValidFilePath(fileName As String) As String
		Dim returnFileName As String = String.Empty
		If fileName.Length > 5 Then ''  Minimum length of a valid file name x.xxx
			Try
				' Create a FileInfo object to test and check the filename
				Dim fileInfo As New FileInfo(fileName)
				returnFileName = fileInfo.FullName ''Get the full path of the file
			Catch ex As Exception
				' Check for invalid characters using Path.GetInvalidFileNameChars
				Dim invalidChars() As Char = Path.GetInvalidFileNameChars()
				If fileName.IndexOfAny(invalidChars) <> -1 Then
					'' Note: fileName is passed by reference and will be modified to replace invalid characters with '_'
					Dim replacedChars As String = ReplaceInputStringWithCharsFound(fileName, invalidChars, "_")
					If Not String.IsNullOrEmpty(replacedChars) Then
						FlashErrorMsg("These invalid characters: \'" & replacedChars & "\' found in file name. Replaced with '_'.")
						returnFileName = fileName
					End If
				Else
					FlashErrorMsg("ERROR: Please check if the entered filename: \"" & fileName & " \ " is invalid.")
				End If
				' Handle exceptions for invalid format
				Console.WriteLine("Error: " & ex.Message)
			End Try
		Else
			FlashErrorMsg("ERROR: Please check if the entered filename: \"" & fileName & " \ " is invalid.")
		End If
		Return returnFileName
	End Function

	''' <summary>
	''' Replaces characters found in the input string with the replacement character. Returns the inputString as reference and the replaced characters as a string
	''' </summary>
	''' <param name="referenceStringToModify">The input string to modify, passed as a reference</param>
	''' <param name="charsToReplace">The characters to replace</param>
	''' <param name="replacementChar">The replacement character</param>
	''' <returns>The replaced characters as a string</returns>
	''' <remarks>Requires the referenceStringToModify to be passed by reference</remarks>
	''' <exception cref="ArgumentNullException">Thrown when the referenceStringToModify is empty or Nothing</exception>
	''' <exception cref="Exception">Thrown when an unknown error occurs</exception>
	Public Function ReplaceInputStringWithCharsFound(ByRef referenceStringToModify As String, charsToReplace() As Char, replacementChar As String) As String
		Dim i As Integer
		Dim replacedChars As String = String.Empty

		For i = LBound(charsToReplace) To UBound(charsToReplace)
			referenceStringToModify = Replace(referenceStringToModify, charsToReplace(i), replacementChar)
			replacedChars.Append(charsToReplace(i))
		Next i

		Return replacedChars
	End Function
#End Region


End Class
