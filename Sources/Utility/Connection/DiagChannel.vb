Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading

Module DiagChannel

    Dim logUtilPtr As IntPtr
    Dim hDiagPhone As SP_HANDLE = Nothing
    Dim portCom As String
    Public _READ_BUFFER_SIZE As ULong = 2048 ''1024
    Public _WRITE_BUFFER_SIZE As ULong = 4096
    Public ChannelBuffer As Byte() = New Byte(_READ_BUFFER_SIZE) {}

    Public Sub DiagConnect(PortCom As String)
        If IsNothing(hDiagPhone) Then
            portCom = PortCom
            hDiagPhone = SP_CreatePhone(logUtilPtr)
            Dim openArgument As New CHANNEL_ATTRIBUTE()
            openArgument.ChannelType = CHANNEL_TYPE.CHANNEL_TYPE_COM
            openArgument.Com.dwPortNum = CUInt(PortCom)
            openArgument.Com.dwBaudRate = 115200 ''921600
            Console.WriteLine("Begin Diag Channel : " & SP_BeginPhoneTest(hDiagPhone, openArgument))
            Logs = "Log/UartComm_COM_" & PortCom & "_" & Date.Today.Year & "_" & Date.Today.Month.ToString("00") & "_" & Date.Today.Day.ToString("00") & "_Rd.bin"
            Main.SharedUI.ReceiverDataWorker.RunWorkerAsync()
        End If
    End Sub

    Public Sub DiagClose()
        Main.SharedUI.ReceiverDataWorker.CancelAsync()
        Main.SharedUI.ReceiverDataWorker.Dispose()

        SP_EndPhoneTest(hDiagPhone)
        SP_ReleasePhone(hDiagPhone)
        ''terminate() ''Test Arry-eng testing
        ''hDiagPhone = Nothing
        ''portCom = Nothing
        Marshal.FreeHGlobal(logUtilPtr)
    End Sub

    Public Function SendReceiveDiag(lpvalue As Byte()) As Integer
        If IsNothing(hDiagPhone) AndAlso Not String.IsNullOrWhiteSpace(portCom) Then
            DiagConnect(portCom)
        End If
#If Not (DEBUG) Then
  Thread.Sleep(15)      'Test Arvind reduced wait for feature phones as they respond immediately
#End If
        ''Thread.Sleep(15)
        RichLogs("fn:SP_SendAndRecvDiagPackage Sending data of size:" & lpvalue.Length, Color.Black, False, False)
        SendReceiveDiag = SP_SendAndRecvDiagPackage(hDiagPhone, lpvalue, lpvalue.Length)
        RichLogs("fn:SP_SendAndRecvDiagPackage received return value::" & SendReceiveDiag, Color.Black, False, False)
    End Function

    Public Sub ReadWriteDiag(lpvalue As Byte())
        If IsNothing(hDiagPhone) AndAlso Not String.IsNullOrWhiteSpace(portCom) Then
            DiagConnect(portCom)
        End If
#If Not (DEBUG) Then
  Thread.Sleep(15)      'Test Arvind reduced wait for feature phones as they respond immediately
#End If
        '        Thread.Sleep(15)
        SP_Write(hDiagPhone, lpvalue, lpvalue.Length)
    End Sub
    Public Sub ReadTask()
        If IsNothing(hDiagPhone) AndAlso Not String.IsNullOrWhiteSpace(portCom) Then
            DiagConnect(portCom)
        End If
        Do
            If Not Main.SharedUI.ReceiverDataWorker.CancellationPending Then
                If Not isPartitionOperation Then
                    ''Delay(2)
                    RichLogs("fn: SharedUI.ReceiverDataWorker.ReadTask().....before SP_Read", Color.Black, False, False)
                    SP_Read(hDiagPhone, ChannelBuffer, _READ_BUFFER_SIZE)
                    RichLogs("fn: SharedUI.ReceiverDataWorker.ReadTask().....after SP_Read", Color.Black, False, False)
                    '' Delay(0.2)
                End If
            Else
                Exit Do
                Return
            End If
        Loop
    End Sub

    Public Function Read_SPRead_Data_ToFile(Optional filename As String = "ReadRet_midsetData.dat", Optional Size As Integer = 0) As Long
        If IsNothing(hDiagPhone) AndAlso Not String.IsNullOrWhiteSpace(portCom) Then
            DiagConnect(portCom)
        End If
        ''DiagConnect(portCom)
        If Size > 0 Then
            ''Dim size As Long = DataReadFlash.LongLength ''4096 ''ChannelBuffer.LongLength
            filename = filename.Trim
            Dim OrgFullName As String = filename
            ''Give a new extionsion to the file to create a temp file
            Dim newExt = filename.Substring(filename.Length - 2)
            filename = filename.Remove(filename.Length - 2)
            IIf(newExt.Equals("_"), "-", "_")

            filename = filename.Append(newExt)
            Dim fileInfo As New IO.FileInfo(filename)

            Dim orgName As String = fileInfo.Name

            ''Dim stream As New FileStream(filename, FileMode.Append, FileAccess.Write)
            Dim stream As FileStream = fileInfo.Open(FileMode.Append, FileAccess.Write)

            Using stream
                Dim buffer As Byte() = New Byte(_READ_BUFFER_SIZE) {}

                Dim toRead As Long = _READ_BUFFER_SIZE 'Data size
                Dim bytesRead As Long = 0
                Dim fileOffset As Long = Size
                Dim timeout As Integer = SPRD_DEFAULT_TIMEOUT
                Do
                    If toRead >= fileOffset Then toRead = fileOffset

                    bytesRead = SP_Read(hDiagPhone, buffer, toRead, timeout)
                    ''end_read_midst(bytesRead, fileOffset)
                    fileOffset -= bytesRead

                    If fileOffset > 0 And bytesRead Then
                        If buffer IsNot Nothing Then
                            stream.Write(buffer, 0, bytesRead)
                            Console.WriteLine("Buffer Data : " & bytesRead)
                            RichLogs("Buffer length" & buffer.Length & " Written Data: " & bytesRead & " of:" & Size, Color.Black, False, False)
                        Else
                            RichLogs("Buffer is NULL Written Data: " & bytesRead & " of:" & Size, Color.Red, True, True)
                        End If
                    Else
                        Exit Do
                    End If

                    ProcessBar1(Size - fileOffset, Size)
                Loop
                ProcessBar1(100)
                stream.Flush()
                stream.Dispose()

                ''  Send_read_end()
            End Using
            RichLogs("OK", Color.Lime, True, True)

            Delay(40)

            ''DiagClose() ''Arry-eng Test Let's not close the phone handle

            If fileInfo.Exists Then
                RichLogs("Parsing ReadRetData : ", Color.Black, True, False)
                Dim Data As Byte() = File.ReadAllBytes(fileInfo.FullName)

                File.WriteAllBytes(OrgFullName, ExtractData(Data))
                RichLogs("Written to data to file " & OrgFullName & ":OK", Color.Lime, True, True)
                Delay(1)
                ''fileInfo.Delete()
            End If
        Else
            RichLogs("None", Color.OrangeRed, True, True)
        End If
    End Function

    Public Sub ReadPartitionChannel(partition As String, size As String)
        If IsNothing(hDiagPhone) AndAlso Not String.IsNullOrWhiteSpace(portCom) Then
            DiagConnect(portCom)
        End If
        ''DiagConnect(portCom)
        Dim i As Integer = 0
        Dim toRead As Long = StrToSize(size) 'Partition Size 1MB
        Dim bytesRead As Long = 4096
        Dim fileOffset As Long = 0

        Do
            fileOffset = bytesRead * i ''Todo - Arvind Whats???


            If fileOffset = toRead - bytesRead Then
                Send_read_midst(bytesRead, fileOffset)
                ProcessBar1(100)
                Send_read_end()
                Exit Do
            End If

            Send_read_midst(bytesRead, fileOffset)
            ProcessBar1(fileOffset, toRead)
            fileOffset += bytesRead ''Todo - Arvind Whats???
            i += 1 ''Todo - Arvind Whats???
        Loop

        RichLogs("OK", Color.Lime, True, True)

        Delay(20)

        ''DiagClose() ''Arry-eng Test Let's not close the phone handle

        If File.Exists(Logs) Then
            RichLogs("Parsing " & partition & " : ", Color.Black, True, False)
            Dim Data As Byte() = File.ReadAllBytes(Logs)
            File.WriteAllBytes(foldersave & "/" & partition & ".img", ExtractData(Data))
            RichLogs("OK", Color.Lime, True, True)
            Delay(1)
            File.Delete(Logs)
        End If

    End Sub
    Public Sub ErasePartitionChannel(partition As String, size As String)
        If IsNothing(hDiagPhone) AndAlso Not String.IsNullOrWhiteSpace(portCom) Then
            DiagConnect(portCom)
        End If
        Dim i As Integer = 0
        Dim toRead As Long = StrToSize(size) 'Partition Size 1MB
        Dim bytesRead As Long = 4096
        Dim fileOffset As Long = 0

        Do
            fileOffset = bytesRead * i


            If fileOffset = toRead - bytesRead Then
                Send_read_midst(bytesRead, fileOffset)
                ProcessBar1(100)
                Send_read_end()
                Exit Do
            End If

            Send_read_midst(bytesRead, fileOffset)
            ProcessBar1(fileOffset, toRead)
            fileOffset += bytesRead
            i += 1
        Loop

        Delay(20)
        ''DiagClose() ''Arry-eng Test Let's not close the phone handle

        RichLogs("OK", Color.Lime, True, True)

        If File.Exists(Logs) Then
            RichLogs("Parsing " & partition & " : ", Color.Black, True, False)
            Dim Data As Byte() = File.ReadAllBytes(Logs)
            File.WriteAllBytes(foldersave & "/" & partition & ".img", ExtractData(Data))
            RichLogs("OK", Color.Lime, True, True)
            Delay(1)
            File.Delete(Logs)
        End If

    End Sub
End Module
