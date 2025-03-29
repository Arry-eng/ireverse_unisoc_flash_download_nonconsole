Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Collections.Concurrent

Public Class BufferedFileWriter
    Inherits TextWriter

    Private _filePath As String
    Private _buffer As New ConcurrentQueue(Of String)()
    Private _flushTimer As Timer
    Private Const FlushIntervalMs As Integer = 1000 ' Flush every 1 second (adjust as needed)
    Private Const BufferSizeThreshold As Integer = 100 ' Flush if buffer reaches this size (adjust as needed)
    Private _isFlushing As Boolean = False
    Private _flushLock As New Object()

    Public Sub New(filePath As String)
        _filePath = filePath
        _flushTimer = New Timer(AddressOf FlushBufferToFile, Nothing, FlushIntervalMs, FlushIntervalMs)
    End Sub

    Public Overrides ReadOnly Property Encoding As Encoding
        Get
            Return Encoding.Unicode
        End Get
    End Property

    Public Overrides Sub Write(value As Char)
        _buffer.Enqueue(value.ToString())
        CheckBufferAndFlush()
    End Sub

    Public Overrides Sub Write(buffer() As Char, index As Integer, count As Integer)
        _buffer.Enqueue(New String(buffer, index, count))
        CheckBufferAndFlush()
    End Sub

    Public Overrides Sub WriteLine(value As String)
        _buffer.Enqueue(String.Format("{0:yyyy-MM-dd HH:mm:ss.fff} - {1}", DateTime.Now, value))
        CheckBufferAndFlush()
    End Sub

    Public Overrides Sub WriteLine(format As String, arg0 As Object)
        _buffer.Enqueue(String.Format("{0:yyyy-MM-dd HH:mm:ss.fff} - {1}", DateTime.Now, String.Format(format, arg0)))
        CheckBufferAndFlush()
    End Sub

    ' Add overloads for other WriteLine formats as needed

    Private Sub CheckBufferAndFlush()
        If _buffer.Count >= BufferSizeThreshold Then
            ' Trigger immediate flush if buffer threshold is reached
            ThreadPool.QueueUserWorkItem(AddressOf FlushBufferToFile)
        End If
    End Sub

    Private Sub FlushBufferToFile(state As Object)
        If Monitor.TryEnter(_flushLock) Then
            Try
                If _isFlushing Then Return
                _isFlushing = True

                Using writer As New StreamWriter(_filePath, True, Encoding.UTF8)
                    Dim line As String
                    While _buffer.TryDequeue(line)
                        writer.WriteLine(line)
                    End While
                End Using
            Finally
                _isFlushing = False
                Monitor.Exit(_flushLock)
            End Try
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing Then
                _flushTimer?.Dispose()
                ' Ensure any remaining buffered data is flushed on dispose
                FlushBufferToFile(Nothing)
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub
End Class
