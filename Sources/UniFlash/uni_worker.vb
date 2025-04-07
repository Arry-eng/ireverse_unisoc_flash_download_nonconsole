Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.IO
Imports System.Xml
Imports UniFlash.PortIO

Module uni_worker
    Public Firmware As String = ""
    Public foldersave As String = ""
    Public PortCom As String = ""
    Public StringXML As String = ""
    Public PACPartitionsTableXMLFile As String = "../../../PAC_partitions.xml"
    Public PhonePartitionsTableXMLFile As String = "../../../Phone_partitions.xml"
    Public LogFileName As String = "../../../DiagnosticFlash.log"
    Public isLogOn As Boolean = True
    Public WorkerMethod As String = ""
    Public USBMethod As String = "Diag Channel"
    Public Logs As String = ""

    Public isPartitionOperation As Boolean = False

    Public totalchecked As Integer = 0
    Public totaldo As Integer = 0

    Public Sub UnisocWorker_DoWork(sender As Object, e As DoWorkEventArgs)
        ''Main.Uncheck_AutoReboot() ''Arry-eng lets keep working with multiple things
        If WorkerMethod = "Download" Then
            isLogOn = False
            If SearchDownloadPort() Then
                ConnectDownload()
                ''DiagClose()
                isLogOn = True
                ''DiagConnect(PortCom)
                Dim retFlag As Boolean = Send_connect()
                RichLogs("Send_connect() Returned:" & retFlag, Color.Black, False, False)
                ''Send_Self_Refresh()
                PhonePartitionsTableXMLFile = "C:\Python\work\repos\Arry-eng\ireverse_unisoc_flash_download_nonconsole\Sources\aPhone_patitions.xml"
                isLogOn = True
                ReadPartitionsTableData(PhonePartitionsTableXMLFile)
                isLogOn = False
            Else
                Return
            End If

            If Main.SharedUI.CkKeepCharge.Checked Then
                Send_keepcharge()
                RichLogs("Keep Charge" & vbTab & " :OK ", Color.Black, True, True)
            End If

        ElseIf WorkerMethod = "Flash" Then
            GetFlashPartition()

            'Reset 

            If Main.SharedUI.CkAutoReboot.Checked Then
                RichLogs("Reboot" & vbTab & vbTab & ":OK ", Color.Black, True, True)
                Send_reset()
            End If

        ElseIf WorkerMethod = "Save PACPartitionsTable" Then
            '' If Not Directory.Exists(Path.GetDirectoryName(Main.SharedUI.TextBoxSaveToPartitionsTableFile.Text)) Then
            ''      Directory.CreateDirectory(Path.GetDirectoryName(Main.SharedUI.TextBoxSaveToPartitionsTableFile.Text))
            ''  End If
            ''Dim input() As String = {Main.SharedUI.TextBoxSaveToPartitionsTableFile.Text, Path.GetDirectoryName(Main.SharedUI.TextBoxSaveToPartitionsTableFiile.Text)}
            Try
                File.WriteAllText(uni_worker.PACPartitionsTableXMLFile, uni_worker.StringXML)
                RichLogs("PACPartitionsTable Written to File: '" & PACPartitionsTableXMLFile & "':OK ", Color.Black, False, True)
            Catch ex As Exception
                RichLogs("ERROR: Writing PACPartitionsTable to File: '" & PACPartitionsTableXMLFile & "'.", Color.Red, False, True)
                UniFlash.Main.FlashErrorMsg("ERROR:Writing PACPartitionsTable to File:'" & PACPartitionsTableXMLFile & "':Exp: " & ex.Message)
            End Try
        ElseIf WorkerMethod = "Save PhonePartitionsTable" Then
            Try
                ''File.WriteAllText(uni_worker.PACPartitionsTableXMLFile, uni_worker.StringXML)
                ReadPartitionsTableData(PhonePartitionsTableXMLFile)

                RichLogs("PhonePartitionsTable Written to File: '" & PhonePartitionsTableXMLFile & "':OK ", Color.Black, False, True)
            Catch ex As Exception
                RichLogs("ERROR: Writing PhonePartitionsTable to File: '" & PhonePartitionsTableXMLFile & "'.", Color.Red, False, True)
                UniFlash.Main.FlashErrorMsg("ERROR:Writing PhonePartitionsTable to File:'" & PhonePartitionsTableXMLFile & "':Exp: " & ex.Message)
            End Try
        ElseIf WorkerMethod = "Read Partition" Then
            GetReadPartition()

            'Reset  

            If Main.SharedUI.CkAutoReboot.Checked Then
                RichLogs("Reboot" & vbTab & vbTab & ":OK ", Color.Black, True, True)
                Send_reset()
            End If

        ElseIf WorkerMethod = "Erase Partition" Then
            GetErasePartition()

            'Reset 

            If Main.SharedUI.CkAutoReboot.Checked Then
                RichLogs("Reboot" & vbTab & vbTab & ":OK ", Color.Black, True, True)
                Send_reset()
            End If

        ElseIf WorkerMethod = "Parse" Then

            'Logs = "Log/UartComm_COM_141_2023_09_26_Rd.bin"
            'Dim Data As Byte() = File.ReadAllBytes("Log/UartComm_COM_141_2023_09_26_Rd.bin")
            'File.WriteAllBytes("Log/boot-bak.img", ExtractData(Data))

            'Dim Data As Byte() = PACExtractor.ExtractPacData(73330112, 20971520)
            'File.WriteAllBytes("Log/boot.img", Data)

        ElseIf WorkerMethod = "PAC Firmware" Then
            If Not Directory.Exists(Path.GetDirectoryName(Main.SharedUI.TxtPacFirmware.Text) & "ImageFiles") Then
                Directory.CreateDirectory(Path.GetDirectoryName(Main.SharedUI.TxtPacFirmware.Text) & "\ImageFiles")
            End If
            Dim input() As String = {Main.SharedUI.TxtPacFirmware.Text, Path.GetDirectoryName(Main.SharedUI.TxtPacFirmware.Text) & "\ImageFiles", "-debug"}
            PACExtractor.StartExtraction(input)
        End If
    End Sub

    Public Function SearchDownloadPort() As Boolean
        RichLogs("Searching USB SPRD Port Device... ", Color.Black, True, False)
        Dim Flag As Boolean = False
        If USBMethod = "Diag Channel" OrElse USBMethod = "Serial Port" Then
            If USBSearchPort() Then
                Flag = True
            End If
        ElseIf USBMethod = "libusb-win32" Then
            If USBWait() Then
                Flag = True
            End If
        End If
        Return Flag
    End Function

    Public Sub ConnectDownload()
        Dim fdl1 As Byte() = Nothing
        Dim fdl1_len As Integer = 0
        Dim fdl1_skip As Integer = 0

        If Main.SharedUI.CkFDL1.Checked Then
            fdl1 = File.ReadAllBytes(Main.SharedUI.TxtFDL1.Text)
            fdl1_len = File.ReadAllBytes(Main.SharedUI.TxtFDL1.Text).Length
            Console.WriteLine("Jumlah Length FDL1 : " & fdl1_len)
        End If

        Dim fdl2 As Byte() = Nothing
        Dim fdl2_len As Integer = 0
        Dim fdl2_skip As Integer = 0

        If Main.SharedUI.CkFDL2.Checked Then
            fdl2 = File.ReadAllBytes(Main.SharedUI.TxtFDL2.Text)
            fdl2_len = File.ReadAllBytes(Main.SharedUI.TxtFDL2.Text).Length
            Console.WriteLine("Jumlah Length FDL2 : " & fdl2_len)
        End If

        If USBMethod = "libusb-win32" Then
            USBConnect()
        ElseIf USBMethod = "Serial Port" Then
            PortOpen(PortCom)
        ElseIf USBMethod = "Diag Channel" Then
            DiagConnect(PortCom)
        End If

        set_chksum_type("crc16")

        RichLogs("Send connect" & vbTab & ": ", Color.Black, True, False)
        RichLogs("Connect command sent", Color.Black, True, True)
        If Send_checkbaud() Then

            If Send_connect() Then
#Region "send_file C++ FDL1"
                If Main.SharedUI.CkFDL1.Checked Then
                    Delay(1)
                    Send_start_fdl(Convert.ToInt32(Main.SharedUI.TxtFDL1Address.Text.Replace("0x", ""), 16), fdl1_len)

                    RichLogs("Sending FDL1    : ", Color.Black, True, False)

                    While (fdl1_len > 0)

                        ProcessBar1(fdl1_skip, fdl1.Length)

                        If fdl1_len > MIDST_SIZE Then
                            Send_midst(TakeByte(fdl1, fdl1_skip, MIDST_SIZE))

                            fdl1_len -= MIDST_SIZE
                            fdl1_skip += MIDST_SIZE
                        Else
                            Send_midst(TakeByte(fdl1, fdl1_skip, fdl1_len))

                            fdl1_len = 0
                        End If

                    End While

                    Send_end()
                    Send_exec()
                    RichLogs("Done", Color.Purple, True, True)

                    '' Send_connect()

                    If Main.SharedUI.CkFDL2.Checked Then
                        ProcessBar2(100, 200)
                    Else
                        ProcessBar2(100, 100)
                    End If

                End If
#End Region

#Region "send_file C++ FDL2"
                If Main.SharedUI.CkFDL2.Checked Then

                    Send_connect()

                    set_chksum_type("add")

                    Send_start_fdl(Convert.ToInt32(Main.SharedUI.TxtFDL2Address.Text.Replace("0x", ""), 16), fdl2_len)

                    RichLogs("Sending FDL2    : ", Color.Black, True, False)

                    While (fdl2_len > 0)

                        ProcessBar1(fdl2_skip, fdl2.Length)

                        If fdl2_len > MIDST_SIZE Then
                            Send_midst(TakeByte(fdl2, fdl2_skip, MIDST_SIZE))
                            fdl2_len -= MIDST_SIZE
                            fdl2_skip += MIDST_SIZE
                        Else
                            Send_midst(TakeByte(fdl2, fdl2_skip, fdl2_len))
                            fdl2_len = 0
                        End If


                    End While

                    Send_end()
                    Send_exec()
                    Send_keepcharge()

                    RichLogs("Done", Color.Purple, True, True)
                    ProcessBar2(200, 200)
                    Main.SharedUI.CkFDLLoaded.Invoke(CType(Sub() Main.SharedUI.CkFDLLoaded.Checked = True, Action))
                End If
#End Region

            Else
                Console.WriteLine("Failed to send ping.")
            End If

        Else
            Console.WriteLine("Failed to send ping.")
        End If


        If USBMethod = "Diag Channel" Then
            DiagClose() ''Arry-eng Test Let's not close the phone handle
            If File.Exists(Logs) Then
                Delay(1)
                File.Delete(Logs)
            End If
        End If

    End Sub
    Public Sub GetFlashPartition()
        Console.WriteLine(StringXML)

        Dim doprosess As Integer = 0
        totaldo = totalchecked
        Dim xr1 As XmlTextReader

        xr1 = New XmlTextReader(New StringReader(StringXML))

        Do While xr1.Read()
            If xr1.NodeType = XmlNodeType.Element AndAlso xr1.Name = "Partition" Then
                Dim partition = xr1.GetAttribute("id")
                Dim startsector = xr1.GetAttribute("startsector")
                Dim endsector = xr1.GetAttribute("endsector")
                Dim size = xr1.GetAttribute("size")
                Dim location = xr1.GetAttribute("location")

                FlashPartition(partition, startsector, endsector, size, location)
                doprosess += 1

                ProcessBar2(doprosess, totaldo)
            End If
        Loop

    End Sub

    Public Sub FlashPartition(partition As String, startsector As ULong, endsector As ULong, size As String, location As String)

        If USBMethod = "Diag Channel" Then
            RichLogs("Connecting to Port: " & PortCom & ": ", Color.Blue, False, True) 'Test Arvind Added for debug info
            DiagConnect(PortCom)
        End If

        Dim PartitionData As Byte()
        Dim PartitionData_len As Long
        Dim PartitionData_writen As Long

        RichLogs("Flashing Partition " & partition & ": ", Color.Black, True, False)

        set_chksum_type("add")
        Dim LogMsg As String = "Transcode could not be disabled"
        If (Send_disable_transcode()) Then
            LogMsg = "Transcode disabled."
        End If
        RichLogs(LogMsg, Color.Blue, False, True) 'Test Arvind Added for debug info

        LogMsg = "Flashing could not be enabled"
        If (Send_enable_flash()) Then
            LogMsg = "Flashing enabled."
        End If
        RichLogs(LogMsg, Color.Blue, False, True) 'Test Arvind Added for debug info

        If Main.SharedUI.CheckBoxErasePartitionBeforeFlashing.Checked Then
            ''CheckBox CheckBoxErasePartitionBeforeFlashing should have been enabled here
            Debug.Assert(Main.SharedUI.CheckBoxErasePartitionBeforeFlashing.Enabled)
            ErasePartition(partition, size) 'Test Arvind Added for testing
            RichLogs("Erase Partition: '" & partition & "' Size:" & size & " :OK ", Color.Black, True, True)
        End If

        If File.Exists(location) Then
            PartitionData = File.ReadAllBytes(location)
            PartitionData_len = PartitionData.Length
            If PartitionData_len > StrToSize(size) Then
                RichLogs("Failed! File size overflow.", Color.Red, True, True)
                Return
            Else
                Send_start_flash(partition, Nothing, PartitionData_len)
            End If
        Else
            PartitionData = PACExtractor.ExtractPacData(startsector, endsector)
            PartitionData_len = PartitionData.Length
            Delay(2)
            Send_start_flash(partition, size)
        End If

        While (PartitionData_len > 0)

            ProcessBar1(PartitionData_writen, PartitionData_len)

            If PartitionData_len > 4096 Then
                Send_midst(TakeByte(PartitionData, PartitionData_writen, 4096))

                PartitionData_len -= 4096
                PartitionData_writen += 4096
            Else
                Send_midst(TakeByte(PartitionData, PartitionData_writen, PartitionData_len))

                PartitionData_len = 0
            End If

        End While

        Send_end()

        RichLogs("OK", Color.Lime, True, True)

        If USBMethod = "Diag Channel" Then
            DiagClose() ''Arry-eng Test Let's not close the phone handle
            If File.Exists(Logs) Then
                Delay(1)
                File.Delete(Logs)
            End If
        End If

    End Sub

    Public Sub GetReadPartition()
        Console.WriteLine(StringXML)

        Dim doprosess As Integer = 0
        totaldo = totalchecked
        Dim xr1 As XmlTextReader

        xr1 = New XmlTextReader(New StringReader(StringXML))

        Do While xr1.Read()
            If xr1.NodeType = XmlNodeType.Element AndAlso xr1.Name = "Partition" Then
                Dim partition = xr1.GetAttribute("id")
                Dim size = xr1.GetAttribute("size")

                ReadPartition(partition, size)
                doprosess += 1

                ProcessBar2(doprosess, totaldo)
            End If
        Loop

    End Sub

    Public Sub ReadPartition(partition As String, size As String)
        Console.WriteLine("Partition Name : " & partition & " Partition size : " & size)

        RichLogs("Reading Partition " & partition & " :", Color.Black, True, False)

        set_chksum_type("add")

        Send_enable_flash()

        Send_read(partition, size)

        'If USBMethod = "Diag Channel" Then
        '    ReadPartitionChannel(partition, size)
        'Else

        Dim stream As New FileStream(foldersave & "\" & partition & ".img", FileMode.Append, FileAccess.Write)
        Using stream
            Dim buffer As Byte() = New Byte(4096) {}

            Dim i As Integer = 0
            Dim toRead As Long = StrToSize(size) 'Partition Size
            Dim bytesRead As Long = 4096
            Dim fileOffset As Long = 0
            Do
                fileOffset = bytesRead * i

                Send_read_midst(bytesRead, fileOffset)

                buffer = DataReadFlash

                If fileOffset = toRead - bytesRead Then

                    If buffer IsNot Nothing Then
                        stream.Write(buffer, 0, buffer.Length)
                        Console.WriteLine("Buffer Data : " & buffer.Length)
                    End If
                    Exit Do
                End If

                If buffer IsNot Nothing Then
                    stream.Write(buffer, 0, buffer.Length)
                    Console.WriteLine("Buffer Data : " & buffer.Length)
                End If

                ProcessBar1(fileOffset, toRead)
                ''fileOffset += bytesRead
                i += 1
            Loop

            ProcessBar1(100)
            stream.Flush()
            stream.Close()

            Send_read_end()

        End Using
        'End If
        RichLogs("OK", Color.Lime, True, True)
    End Sub

    Private Sub ReadPartitionsTableData(fileName As String)
        If USBMethod = "Diag Channel" Then
            DiagConnect(PortCom)
        End If

        Console.WriteLine("Getting PartitionsTable Info on port : " & PortCom)

        RichLogs("Getting PartitionsTable Info on port :" & PortCom, Color.Black, True, False)

        set_chksum_type("add")
        ''Send_Self_Refresh()
        Send_enable_flash()
        ''Read_ack()

        Dim size As Long = Send_readPartitionsTableToFile(fileName)
        Send_read(" ", size.ToString)
        '' Dim resFlag As Boolean = False
        ''resFlag = Read_ack()'Data already read in SendRead()

    End Sub

    Public Sub GetErasePartition()
        Console.WriteLine(StringXML)

        Dim doprosess As Integer = 0
        totaldo = totalchecked
        Dim xr1 As XmlTextReader

        xr1 = New XmlTextReader(New StringReader(StringXML))

        Do While xr1.Read()
            If xr1.NodeType = XmlNodeType.Element AndAlso xr1.Name = "Partition" Then
                Dim partition = xr1.GetAttribute("id")
                Dim size = xr1.GetAttribute("size")

                ErasePartition(partition, size)
                doprosess += 1

                ProcessBar2(doprosess, totaldo)
            End If
        Loop

    End Sub

    Public Sub ErasePartition(partition As String, size As String)
        If USBMethod = "Diag Channel" Then
            DiagConnect(PortCom)
        End If

        Console.WriteLine("Partition Name : " & partition & " Partition size : " & size)

        RichLogs("Erasing Partition '" & partition & "' Size:" & size & ": ", Color.Black, True, False)

        set_chksum_type("add")

        Send_enable_flash()

        Send_erase(partition, size)

        RichLogs("OK", Color.Lime, True, True)

        If USBMethod = "Diag Channel" Then
            DiagClose()  ''Arry-eng Test Let's not close the phone handle
            If File.Exists(Logs) Then
                Delay(1)
                File.Delete(Logs)
            End If
        End If
    End Sub

    Public Sub UnisocWorker_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs)
        WorkerMethod = ""
        RichLogs("", Color.Black, True, True)
        RichLogs("_____________________________________________________________________________", Color.Black, True, True)
        RichLogs("Progress is completed", Color.Black, True, True)
    End Sub

    Public Sub ReceiverDataWorker_DoWork(sender As Object, e As DoWorkEventArgs)
        ReadTask()
        Console.WriteLine("fn:ReceiverDataWorker_DoWork DataWorker starting ..................")
    End Sub

    Public Sub ReceiverDataWorker_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs)
        Console.WriteLine("fn:ReceiverDataWorker_RunWorkerCompleted DataWorker stopping ..................")
        DiagClose()
    End Sub
End Module
