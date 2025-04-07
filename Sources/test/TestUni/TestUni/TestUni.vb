Imports System.IO
Imports System.Text

Module TestUni
	Friend THDLC_HEADER As Integer = &H7E
	Friend THDLC_ESCAPE As Integer = &H7D
	Friend Enum TBSL
		TCMD_ERASE_FLASH = &HA
	End Enum
End Module

Module test
	Friend Const TSPRD_DEFAULT_TIMEOUT As Integer = 2000

	Private Function StrToSize(ByVal str As String) As ULong
		Dim n As ULong
		n = str.Replace("K", "").Replace("M", "").Replace("G", "")

		Dim shl As Integer = 0
		If str.EndsWith("K") Then
			shl = 10
		ElseIf str.EndsWith("M") Then
			shl = 20
		ElseIf str.EndsWith("G") Then
			shl = 30
		Else
			Throw New Exception("unknown size suffix")
		End If

		If shl <> 0 Then
			Dim tmp As Long = CLng(n)
			tmp >>= 63 - shl
			If tmp <> 0 AndAlso tmp <> -1 Then
				Throw New Exception("size overflow on multiply")
			End If
		End If

		Return n << shl
	End Function

	Private Function Generate_packet(command As Integer, Optional data As Byte() = Nothing) As Byte()
		Dim packet As New List(Of Byte)()
		packet.AddRange(BitConverter.GetBytes(CType(command, UInt16)))

		If data IsNot Nothing AndAlso data.Length > 0 Then
			packet.AddRange(BitConverter.GetBytes(CType(data.Length, UInt16)))
			packet.AddRange(data)
		Else
			packet.AddRange(BitConverter.GetBytes(CType(0, UInt16)))
		End If

		Dim chksum As Integer = packet.Sum(Function(b) b)
		packet.AddRange(BitConverter.GetBytes(CType(chksum, UInt16)))
		Return packet.ToArray()
	End Function

	Private Function Send_data(data As Byte(), Optional timeout As Integer = Nothing) As Boolean
		' Simulate sending data and receiving an acknowledgment
		Console.WriteLine("Data sent: " & BitConverter.ToString(data).Replace("-", " "))
		Return True
	End Function

	Private Function Send_erase(Partition As String, Optional size As String = "1M", Optional partitionsize As ULong = 0) As Boolean
		Dim asize As ULong

		If partitionsize > 0 Then
			asize = partitionsize
		Else
			asize = StrToSize(size)
		End If

		Dim Taken As Byte() = BitConverter.GetBytes(asize) '' 64 bits - 8 bytes

		Dim byteA As Byte() = Encoding.Unicode.GetBytes(Partition) ''Converts each alphabet of the string into two bytes (unicode). Length of Bytes array is string-length * 2
		Dim byteC As Byte() = Taken.Take(4).ToArray() ''Stores first 4 bytes of the partition size - 32 bits - 4 bytes - Can store numbers of bit for up to 2GiBits size 1024 (1k)*1024(1M)*1023(1Gi)*2=2,14,74,83,648(2Gibits)

		Dim lengthA As Integer = byteA.Length
		Dim lengthC As Integer = byteC.Length

		Dim totalLength As Integer = 76 '' 4 bits less then 10 bytes(80 bits)
		Dim lengthB As Integer = totalLength - (lengthA + lengthC)

		Dim byteB(lengthB - 1) As Byte

		For i As Integer = 0 To lengthB - 1
			byteB(i) = 0
		Next
		''store the partition at beginning and size in the end 4 bytes. Fill all bytes in between with zeros.
		Dim resultBytes As Byte() = New Byte(lengthA + lengthB + lengthC - 1) {}
		Array.Copy(byteA, 0, resultBytes, 0, lengthA)
		Array.Copy(byteB, 0, resultBytes, lengthA, lengthB)
		Array.Copy(byteC, 0, resultBytes, lengthA + lengthB, lengthC)

		Dim Tosend As Byte() = Generate_packet(TestUni.TBSL.TCMD_ERASE_FLASH, resultBytes)

		Console.WriteLine(" ")
		Console.WriteLine(StrToSize(size) & " " & BitConverter.ToString(byteC).Replace("-", " "))
		Console.WriteLine("Erase Partition Data : " & BitConverter.ToString(Tosend).Replace("-", " ")) '' prints binary of "\nLtest_partition" +(First 16 bit -Command - &HA-"ASCII-\n"(CMD_ERASE_FLASH),
		''																								''next 16 bit-76-"ASCII-L" size of data, data in binary (test_partition),
		''																								''zeors and last 32 bits are 16 bits-10 00 (1M-size of partition and 16 bits checksum -5F 06
		''Colsole out put is 
		''1048576 00 00 10 00
		''Erase Partition Data : 0A 00 4C 00 74 00 65 00 73 00 74 00 5F 00 70 00 61 00 72 00 74 00 69 00 74 00 69 00 6F 00 6E 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 5F 06

		Return Send_data(Tosend)
	End Function

	Sub Main()
		' Test the Send_erase function
		Dim partition As String = "test_partition"
		Dim size As String = "1M"
		Dim partitionsize As ULong = 0

		Dim result As Boolean = Send_erase(partition, size, partitionsize)
		Console.WriteLine("Send_erase result: " & result)
	End Sub
End Module
