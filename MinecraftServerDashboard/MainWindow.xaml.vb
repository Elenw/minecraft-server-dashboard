﻿Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Net.NetworkInformation

Class MainWindow

#Region "MainWindow events"

    Sub New()
        ' This call is required by the designer.
        InitializeComponent()

        ' Add basic navigation keyboard shortcuts (Alt+Left, Alt+Right)
        AppNav_GoBack.InputGestures.Add(New KeyGesture(Key.Left, ModifierKeys.Alt))
        AppNav_GoForward.InputGestures.Add(New KeyGesture(Key.Right, ModifierKeys.Alt))

        Me.CommandBindings.Add(New CommandBinding(NavigationCommands.BrowseBack, AddressOf AppNav_GoBackExecuted))
        Me.CommandBindings.Add(New CommandBinding(NavigationCommands.BrowseForward, AddressOf AppNav_GoForwardExecuted))

        ' Allow this object to be accessed globally in project
        MyApp.MyMainWindow = Me
        ' Create instances of all required objects
        MyApp.MyServer = New ServerManager()

        MyApp.navpageDashboard = New PageDashboard
        MyApp.navpagePlayers = New PagePlayers
        MyApp.navpageWorld = New pageWorld
        MyApp.navpageConsole = New PageConsole
        MyApp.navpageScheduler = New PageScheduler
        MyApp.navPageCBconfig = New PageConfig

        ' Add each page to their respective tabpage
        FrameDashboard.Content = navpageDashboard
        FramePlayers.Content = navpagePlayers
        FrameWorld.Content = navpageWorld
        FrameConsole.Content = navpageConsole
        FrameScheduler.Content = navpageScheduler
        FrameConfigHome.Content = navPageCBconfig

        ' Set up data bindings
        Me.DataContext = MyMainWindowProperties
        MyAppMenu.DataContext = MyServer

        ' Load saved scheduled tasks from stored settings
        For Each task In MyUserSettings.tasksStore.TaskList
            navpageScheduler.AddTask(task)
        Next
    End Sub

    'Data bindings
    Public MyMainWindowProperties As New MainWindowViewModel

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' NOT WORKING (Seems to be causing app crash!?)
        '              AddHandler NetworkChange.NetworkAvailabilityChanged, New NetworkAvailabilityChangedEventHandler(AddressOf NetworkChange_NetworkAvailabilityChanged)

        ' Start a background task to retrieve the machine's external IP address
        Dispatcher.Invoke(New Action( _
                                      Function()
                                          UpdateIP()
                                          Return True
                                      End Function))

        ' Check for first-time app start
        If Not My.Computer.FileSystem.FileExists(MyUserSettings.settingsStore.Jarfile) Then
            ' Display welcome screen
            Using l As New Welcome
                Dim x As New OverlayDialog
                x.DisplayConfig(l)
            End Using
        End If

    End Sub

    Private Sub MainWindow_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        MyUserSettings.Save()
    End Sub

#End Region

#Region "Navigation (Back/Formward)"

    ' Map keyboard shortcuts
    Public Shared AppNav_GoBack As RoutedCommand = New RoutedCommand()
    Public Shared AppNav_GoForward As RoutedCommand = New RoutedCommand()

    Sub AppNav_GoBackExecuted()
        Dim actionComplete As Boolean = False
        If Not (navPageCBconfig.viewSuperOverlay Is Nothing) Then
            If Not (navPageCBconfig.viewSuperOverlay.isClosed) Then

                navPageCBconfig.viewSuperOverlay.tryClose()
                actionComplete = True
            End If
        End If

        If Not actionComplete Then
            If navDashboard.IsEnabled Then
                BackButton_Click()
            End If
        End If
    End Sub

    Sub AppNav_GoForwardExecuted()
        If navDashboard.IsEnabled Then
            ForwardButton_Click()
        End If
    End Sub

    ' Keep a history of tab pages navigated
    Dim NavigationHistoryList As New List(Of Integer)
    Dim NavigationHistoryList_CurrentIndex As Integer = -1 ' Start with empty history

    Dim _IsInNavigation As Boolean = False ' Ensure history events are recorded only once
    Private Sub BackButton_Click() Handles BackButton1.Click
        If NavigationHistoryList_CurrentIndex > 0 Then ' Whilst able to go backwards in history list
            _IsInNavigation = True
            NavigationHistoryList_CurrentIndex -= 1 ' Go back one
            navDashboard.SelectedIndex = NavigationHistoryList(NavigationHistoryList_CurrentIndex)
            UpdateDashboardTabHeaderLabel()
        End If
    End Sub

    Private Sub ForwardButton_Click() Handles BackButton1_Copy.Click
        If (NavigationHistoryList.Count - 1) > NavigationHistoryList_CurrentIndex Then ' Whilst able to go forwards in history list
            _IsInNavigation = True
            NavigationHistoryList_CurrentIndex += 1 ' Go forward one
            navDashboard.SelectedIndex = NavigationHistoryList(NavigationHistoryList_CurrentIndex)
            UpdateDashboardTabHeaderLabel()
        End If
    End Sub

    Private Sub navDashboard_SelectionChanged(sender As System.Windows.Controls.TabControl, e As SelectionChangedEventArgs) Handles navDashboard.SelectionChanged
        If _IsInNavigation = True Then
            ' Don't record again if already
            _IsInNavigation = False
            Exit Sub
        End If



        If TypeOf e.Source Is System.Windows.Controls.TabControl Then
            UpdateDashboardTabHeaderLabel()

            ' Add new history record
            NavigationHistoryList_CurrentIndex += 1
            NavigationHistoryList.Add(navDashboard.SelectedIndex)

            
        End If

    End Sub

    Sub UpdateDashboardTabHeaderLabel()
        Dim thisHeader As Object = navDashboard.SelectedItem.Header

        ' Update the current 'heading' UI label (e.g. "dashboard", "players", "world")
        For index As Integer = thisHeader.Children.Count - 1 To 0 Step -1
            If TypeOf thisHeader.Children(index) Is TextBlock Then
                MyMainWindowProperties.navTitle = CType(thisHeader.Children(index), TextBlock).Text

                Select Case MyMainWindowProperties.navTitle
                    Case "World"
                        navpageWorld.RefreshPageData()
                End Select

                Exit Sub ' Exit loop once the heading label has been found in the heading controls
            End If
        Next
    End Sub
#End Region

#Region "User attempt to close whilst server process still running"

    ' Do NOT allow exit whilst server is still running, as user will not be able to stop/interact with server console anymore!

    Friend isExitWindowOverlay As Boolean = False
    Property TryQuitOnExit As Boolean = False

    Private Sub Window_Closing(sender As Object, e As CancelEventArgs)
        If Not TryQuitOnExit Then
            If MyMainWindowProperties.MainWindowOverlay = MainWindowViewModel.OverlayShownType.StandardDialog Then
                thisOverlayState.RaiseCloseWindowEventEvent()
                e.Cancel = True ' Don't allow the application to exit whilst a dialog is being displayed
                Exit Sub
            End If
        End If

        If MyServer.ServerIsOnline Then
            e.Cancel = True ' Don't allow the application to exit whilst the game server is running.
            If Not isExitWindowOverlay Then
                MyMainWindowProperties.MainWindowOverlay = MainWindowViewModel.OverlayShownType.StandardDialog
                Dim M As New ExitWarning
                FormControls.Children.Add(M)
                OverlayOpened()
                isExitWindowOverlay = True
            End If
        End If
    End Sub

#End Region

#Region "UI"

#Region "Dialogs & Overlay form controls"

    Friend thisOverlayState As New MyOverlayState

    Class MyOverlayState
        Sub RaiseCloseWindowEventEvent()
            RaiseEvent isOverlay_CloseWindowEvent()
        End Sub

        Event isOverlay_CloseWindowEvent()
    End Class

    Sub OverlayOpened()
        'Disable MainWindow controls when another dialog is open
        navPageCBconfig.IsEnabled = False
        navPageCBconfig.FormControls.Visibility = Windows.Visibility.Hidden
    End Sub

    Sub OverlayClosed()
        'Check if another window still open
        If MyMainWindowProperties.MainWindowOverlay = MainWindowViewModel.OverlayShownType.StandardDialog Then
            Exit Sub
        ElseIf isExitWindowOverlay Then
            Exit Sub
        Else
            'Re-enable MainWindow controls once dialog closed
            navPageCBconfig.IsEnabled = True
            navPageCBconfig.FormControls.Visibility = Windows.Visibility.Visible
        End If
    End Sub

#End Region

#Region "Menu items"

    Private Sub StartServer_Click(sender As Object, e As RoutedEventArgs)
        MyServer.StartServer()
    End Sub

    Private Sub StopServer_Click(sender As Object, e As RoutedEventArgs)
        MyServer.StopServer()
    End Sub

    Private Sub ReloadServer_Click(sender As Object, e As RoutedEventArgs)
        MyServer.SendCommand("reload")
    End Sub

    Private Sub ForceStopServer_Click(sender As Object, e As RoutedEventArgs)
        If MessageBox.Show("Are you sure you want to force quit the server? This will disconnect all players, and all unsaved data will be lost.", "Force stop server", MessageBoxButton.YesNo) = MessageBoxResult.Yes Then
            MyServer.KillServer()
        End If
    End Sub

    Private Sub AboutMenuItem_Clicked(sender As Object, e As RoutedEventArgs)
        Using l As New AboutWindow
            Dim x As New OverlayDialog
            x.DisplayConfig(l)
        End Using
    End Sub

    Private Sub ShowHideDashboardTabs_Click(sender As Object, e As RoutedEventArgs) Handles ShowHideDashboardTabs.Click
        Dim NewState As Windows.Visibility
        If tabOverview.Visibility = Windows.Visibility.Visible Then
            'CType(sender, MenuItem).Header = "Show dashboard navigation"
            CType(sender, MenuItem).IsChecked = True
            NewState = Windows.Visibility.Collapsed
        Else
            'CType(sender, MenuItem).Header = "Toggle dashboard navigation"
            CType(sender, MenuItem).IsChecked = False
            NewState = Windows.Visibility.Visible
        End If

        For index As Integer = navDashboard.Items.Count - 1 To 0 Step -1

            If TypeOf CType(navDashboard, System.Windows.Controls.TabControl).Items(index) Is TabItem Then

                CType((navDashboard.Items(index)), TabItem).Visibility = NewState

            End If

        Next
    End Sub

    Private Sub btn_ShowServerProperties_Screen(sender As Object, e As RoutedEventArgs)
        navPageCBconfig.Go_ServerPropertiesSettings()
    End Sub

    Private Sub btn_BackendSetUp(sender As Object, e As RoutedEventArgs)
        navPageCBconfig.Go_BackendSetup()
    End Sub

    Private Sub mnuDoBackupNow_Click(sender As Object, e As RoutedEventArgs) Handles mnuDoBackupNow.Click
        navpageWorld.DoBackupScreen()
    End Sub

    Private Sub btnJoinNewPlayers_Click()
        Dim e As New JoinNewPlayers
        Dim M As New OverlayDialog
        M.DisplayConfig(e)
    End Sub

    Private Sub MenuItem_Click(sender As Object, e As RoutedEventArgs)
        System.Diagnostics.Process.Start("http://MinecraftForum.net")
    End Sub

    Private Sub MenuItem_Click_2(sender As Object, e As RoutedEventArgs)
        System.Diagnostics.Process.Start("http://MinecraftWiki.net")
    End Sub

    Private Sub btn_ExploreServerDir(sender As Object, e As RoutedEventArgs)
        ExploreServerDir()
    End Sub

    Public Sub DoManualSrvPropertiesEdit() Handles MenuItem1.Click
        If Not My.Computer.FileSystem.FileExists(MyServer.MyStartupParameters.ServerProperties) Then
            Dim n As New MessageWindow(MyMainWindow, "", "Please launch the server at least once.", "Oops", "large")
        Else
            Try
                System.Diagnostics.Process.Start(MyUserSettings.UserSettings_DefaultTextEditor, MyServer.MyStartupParameters.ServerProperties)
            Catch ex As Exception
                Dim n As New MessageWindow(MyMainWindow, "", "Could not start the text editor:" & vbNewLine & """" & MyUserSettings.UserSettings_DefaultTextEditor & """" & vbNewLine & "Set the correct executable in the Dashboard app settings screen, or just leave it empty for notepad :)", "Error", "large")
            End Try
        End If
    End Sub

#End Region

#End Region

#Region "Network state change"
    Sub NetworkChange_NetworkAvailabilityChanged(sender As Object, e As NetworkAvailabilityEventArgs)
        'When 'Network Environment changed', attempt to update IP address info
        Debug.Print("Network Environment changed")
        ' Start process in background
        Dispatcher.Invoke(New Action( _
                                              Function()
                                                  Try
                                                      Dim newthread As New System.Threading.Thread(AddressOf UpdateIP)
                                                      newthread.Start()
                                                  Catch ex As Exception
                                                  End Try
                                                  Return True
                                              End Function))
    End Sub

    Public WithEvents UpdateExtIP_thread As New System.ComponentModel.BackgroundWorker

    Sub UpdateIP()
        If Not UpdateExtIP_thread.IsBusy Then
            UpdateExtIP_thread.RunWorkerAsync()
        End If
    End Sub

    Private Sub UpdateExtIP_thread_DoWork(sender As Object, e As DoWorkEventArgs) Handles UpdateExtIP_thread.DoWork
        e.Result = GetExternalIP()
    End Sub

    Private Sub UpdateExtIP_thread_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles UpdateExtIP_thread.RunWorkerCompleted
        Dispatcher.Invoke(New Action( _
                         Function()
                             MyExternalIPAddressViewModel.ExternalIP = e.Result
                             Return True
                         End Function))
    End Sub

#End Region

#Region "Helpers"
    Public Function ExploreServerDir() As Boolean
        If My.Computer.FileSystem.DirectoryExists(MyServer.MyStartupParameters.ServerPath) Then
            System.Diagnostics.Process.Start("explorer.exe", MyServer.MyStartupParameters.ServerPath)
            Return True
        Else
            Dim n As New MessageWindow(MyMainWindow, "", "Please configure the server backend first.")
            Return False
        End If
    End Function
#End Region

End Class