using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Student.Core.Services;
using LabManager.Teacher.Core.Services;

namespace LabManager.Network.Tests;

public class DiscoveryTests
{
    [Fact]
    public async Task TeacherAndStudent_CanDiscoverEachOther_OverUdpLoopback()
    {
        // Arrange
        using var classroom = new ClassroomManager
        {
            OfflineThreshold = TimeSpan.FromSeconds(2)
        };
        using var student = new StudentCoordinator();

        StudentNode? discoveredStudent = null;
        var tcs = new TaskCompletionSource<StudentNode>();

        classroom.OnStudentJoined += node =>
        {
            discoveredStudent = node;
            tcs.TrySetResult(node);
        };

        // Act
        student.Start();
        classroom.StartDiscovery(teacherName: "Thầy Hùng", roomName: "Phòng Máy 01");

        // Assert - chờ nhận diện tối đa 6 giây
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(6000));
        Assert.Equal(tcs.Task, completedTask);

        Assert.NotNull(discoveredStudent);
        Assert.Equal(MachineStatus.Online, discoveredStudent.Status);
        Assert.False(string.IsNullOrWhiteSpace(discoveredStudent.MachineName));
        Assert.False(string.IsNullOrWhiteSpace(discoveredStudent.CurrentUser));
        Assert.False(string.IsNullOrWhiteSpace(discoveredStudent.IpAddress));

        // Dừng Student và kiểm tra xem Teacher có phát hiện offline không
        var offlineTcs = new TaskCompletionSource<StudentNode>();
        classroom.OnStudentOffline += node =>
        {
            if (node.Id == discoveredStudent.Id)
            {
                offlineTcs.TrySetResult(node);
            }
        };

        if (discoveredStudent.Status == MachineStatus.Offline)
        {
            offlineTcs.TrySetResult(discoveredStudent);
        }

        student.Stop();

        var offlineTask = await Task.WhenAny(offlineTcs.Task, Task.Delay(8000));
        Assert.Equal(offlineTcs.Task, offlineTask);

        Assert.Equal(MachineStatus.Offline, discoveredStudent.Status);
    }
}
