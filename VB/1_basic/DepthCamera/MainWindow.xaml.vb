﻿Imports System
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports Microsoft.Kinect

Partial Public Class MainWindow
    Inherits Window

    Private ReadOnly Bgr32BytesPerPixel As Integer = PixelFormats.Bgr32.BitsPerPixel / 8

    ''' <summary>
    ''' コンストラクタ
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub New()
        Try
            InitializeComponent()

            ' Kinectが接続されているかどうかを確認する
            If (KinectSensor.KinectSensors.Count = 0) Then
                Throw New Exception("Kinectを接続してください")
            End If

            ' Kinectの動作を開始する
            Call StartKinect(KinectSensor.KinectSensors(0))
        Catch ex As Exception
            MessageBox.Show(ex.Message)
            Me.Close()
        End Try
    End Sub

    ''' <summary>
    ''' Kinectの動作を開始する
    ''' </summary>
    ''' <param name="kinect"></param>
    ''' <remarks></remarks>
    Private Sub StartKinect(ByVal kinect As KinectSensor)
        ' RGBカメラを有効にして、フレーム更新イベントを登録する
        kinect.ColorStream.Enable()
        AddHandler kinect.ColorFrameReady, AddressOf kinect_ColorFrameReady

        ' 距離カメラを有効にして、フレーム更新イベントを登録する
        kinect.DepthStream.Enable()
        AddHandler kinect.DepthFrameReady, AddressOf kinect_DepthFrameReady

        ' Kinectの動作を開始する
        kinect.Start()

        ' defaultモードとnearモードの切り替え
        Me.comboBoxRange.Items.Clear()
        For Each range In [Enum].GetValues(GetType(DepthRange))
            Me.comboBoxRange.Items.Add(range.ToString)
        Next
        Me.comboBoxRange.SelectedIndex = 0
    End Sub

    ''' <summary>
    ''' Kinectの動作を停止する
    ''' </summary>
    ''' <param name="kinect"></param>
    ''' <remarks></remarks>
    Private Sub StopKinect(ByVal kinect As KinectSensor)
        If kinect IsNot Nothing Then
            If kinect.IsRunning Then
                ' フレーム更新イベントを削除する
                RemoveHandler kinect.ColorFrameReady, AddressOf kinect_ColorFrameReady
                RemoveHandler kinect.DepthFrameReady, AddressOf kinect_DepthFrameReady

                ' Kinectの停止と、ネイティブリソースを解放する
                kinect.Stop()
                kinect.Dispose()

                Me.imageDepth.Source = Nothing
                Me.imageRgb.Source = Nothing
            End If
        End If
    End Sub

    ''' <summary>
    ''' RGBカメラのフレーム更新イベント
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub kinect_ColorFrameReady(sender As Object,
                                       e As ColorImageFrameReadyEventArgs)
        Try
            ' RGBカメラのフレームデータを取得する
            Using colorFrame As ColorImageFrame = e.OpenColorImageFrame
                If colorFrame IsNot Nothing Then
                    ' RGBカメラのピクセルデータを取得する
                    Dim colorPixel(colorFrame.PixelDataLength - 1) As Byte
                    colorFrame.CopyPixelDataTo(colorPixel)

                    ' ピクセルデータをビットマップに変換する
                    Me.imageRgb.Source = BitmapSource.Create(colorFrame.Width,
                                                             colorFrame.Height,
                                                             96,
                                                             96,
                                                             PixelFormats.Bgr32,
                                                             Nothing,
                                                             colorPixel,
                                                             colorFrame.Width * colorFrame.BytesPerPixel)
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 距離カメラのフレーム更新イベント
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub kinect_DepthFrameReady(sender As Object,
                                       e As DepthImageFrameReadyEventArgs)
        Try
            ' センサーのインスタンスを取得する
            Dim kinect As KinectSensor = CType(sender, KinectSensor)
            If kinect Is Nothing Then
                Exit Sub
            End If

            ' 距離カメラのフレームデータを取得する
            Using depthFrame As DepthImageFrame = e.OpenDepthImageFrame
                If depthFrame IsNot Nothing Then
                    ' 距離データを画像化して表示
                    Me.imageDepth.Source = BitmapSource.Create(depthFrame.Width,
                                                               depthFrame.Height,
                                                               96,
                                                               96,
                                                               PixelFormats.Bgr32,
                                                               Nothing,
                                                               ConvertDepthColor(kinect, depthFrame),
                                                               depthFrame.Width * Bgr32BytesPerPixel)
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 距離データをカラー画像に変換する
    ''' </summary>
    ''' <param name="kinect"></param>
    ''' <param name="depthFrame"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function ConvertDepthColor(kinect As KinectSensor, depthFrame As DepthImageFrame) As Byte()
        Dim colorStream As ColorImageStream = kinect.ColorStream
        Dim depthStream As DepthImageStream = kinect.DepthStream

        ' 距離カメラのピクセルごとのデータを取得する
        Dim depthPixel(depthFrame.PixelDataLength - 1) As Short
        depthFrame.CopyPixelDataTo(depthPixel)

        ' 距離カメラの座標に対応するRGBカメラの座標を取得する(座標合わせ)
        Dim colorPoint(depthFrame.PixelDataLength - 1) As ColorImagePoint
        kinect.MapDepthFrameToColorFrame(depthStream.Format, depthPixel,
                                         colorStream.Format, colorPoint)

        Dim depthColor(depthFrame.PixelDataLength * Bgr32BytesPerPixel - 1) As Byte
        For index As Integer = 0 To depthPixel.Length - 1
            ' 距離カメラのデータから、プレイヤーIDと距離を取得する
            Dim distance As Integer = depthPixel(index) >> DepthImageFrame.PlayerIndexBitmaskWidth

            '' 変換した結果が、フレームサイズを超えることがあるため、小さいほうを使う
            Dim x As Integer = Math.Min(colorPoint(index).X, colorStream.FrameWidth - 1)
            Dim y As Integer = Math.Min(colorPoint(index).Y, colorStream.FrameHeight - 1)

            '' 動作が遅くなる場合、MapDepthFrameToColorFrame を外すと速くなる場合が
            '' あります。外す場合のx,yはこちらを使用してください。
            'Dim x As Integer = index Mod depthFrame.Width
            'Dim y As Integer = index / depthFrame.Width

            Dim colorIndex As Integer = ((y * depthFrame.Width) + x) * Bgr32BytesPerPixel

            If distance = depthStream.UnknownDepth Then
                ' サポート外 0-40cm
                depthColor(colorIndex) = 0
                depthColor(colorIndex + 1) = 0
                depthColor(colorIndex + 2) = 255
            ElseIf distance = depthStream.TooNearDepth Then
                ' 近すぎ 40cm-80cm(default mode)
                depthColor(colorIndex) = 0
                depthColor(colorIndex + 1) = 255
                depthColor(colorIndex + 2) = 0
            ElseIf distance = depthStream.TooFarDepth Then
                ' 遠すぎ 3m(Near),4m(Default)-8m
                depthColor(colorIndex) = 255
                depthColor(colorIndex + 1) = 0
                depthColor(colorIndex + 2) = 0
            Else
                ' 有効な距離データ
                depthColor(colorIndex) = 0
                depthColor(colorIndex + 1) = 255
                depthColor(colorIndex + 2) = 255
            End If
        Next

        Return depthColor
    End Function

    ''' <summary>
    ''' 距離カメラの通常/近接モード変更イベント
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub comboBoxRange_SelectionChanged(sender As System.Object,
                                               e As System.Windows.Controls.SelectionChangedEventArgs)
        Try
            KinectSensor.KinectSensors(0).DepthStream.Range = CType(comboBoxRange.SelectedIndex, DepthRange)
        Catch ex As Exception
            comboBoxRange.SelectedIndex = 0
        End Try
    End Sub

    ''' <summary>
    ''' Windowsが閉じられるときのイベント
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub Window_Closing(sender As System.Object,
                               e As System.ComponentModel.CancelEventArgs)
        Call StopKinect(KinectSensor.KinectSensors(0))
    End Sub
End Class
