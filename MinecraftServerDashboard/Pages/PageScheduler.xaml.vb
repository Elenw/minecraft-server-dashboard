﻿Class PageScheduler

    Friend ListOfSchedulerTaskItem As New List(Of SchedulerTaskItem)

    Public Sub AddTask(newTask As TaskScheduler.Task)
        Dim newTaskItem As New SchedulerTaskItem(newTask)
        UITasksList.Children.Insert(0, newTaskItem) ' Add to top
        ListOfSchedulerTaskItem.Add(newTaskItem)

        AddHandler newTaskItem.taskIsEnabledStateChanged, AddressOf UpdateUI
        AddHandler newTaskItem.taskRemoved, AddressOf taskRemoved

        UpdateUI()
    End Sub

    Private Sub taskRemoved(taskItem As SchedulerTaskItem)
        MyApp.taskScheduler.removeTask(taskItem.Task)
        ListOfSchedulerTaskItem.Remove(taskItem)

        UITasksList.Children.Remove(taskItem)
        UpdateUI()
    End Sub

#Region "UI"
    Private Sub addNewTaskButton_Click(sender As Object, e As RoutedEventArgs)
        Dim newTask As New TaskScheduler.Task
        newTask.ID = MyApp.taskScheduler.nextTaskID
        AddTask(newTask)
    End Sub

    Private Sub UpdateUI()
        If UITasksList.Children.Count = 0 Then
            InfoOverlay.Visibility = Windows.Visibility.Visible
        Else
            InfoOverlay.Visibility = Windows.Visibility.Collapsed
        End If

        If MyApp.taskScheduler.schedulerRunning Then
            ' No changes will be made to the scheduler if it is already running
            Exit Sub
        Else
            Dim count As Integer = 0
            For Each child In UITasksList.Children
                If TypeOf (child) Is SchedulerTaskItem Then
                    If CType(child, SchedulerTaskItem).chkboxIsTaskEnabled.IsChecked Then
                        count += 1
                    End If
                End If
            Next
            lblEnabledCount.Text = count
        End If
    End Sub

#End Region

    End Class
