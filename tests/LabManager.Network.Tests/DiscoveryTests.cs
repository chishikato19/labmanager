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

    [Fact]
    public void ParseIpInput_CorrectlyParsesVariousFormats()
    {
        // 1. Single IP
        var single = ClassroomManager.ParseIpInput("192.168.1.106");
        Assert.Single(single);
        Assert.Equal("192.168.1.106", single[0].ToString());

        // 2. Range with hyphen: 192.168.1.1 - 192.168.1.5
        var rangeFull = ClassroomManager.ParseIpInput("192.168.1.1 - 192.168.1.5");
        Assert.Equal(5, rangeFull.Count);
        Assert.Equal("192.168.1.1", rangeFull[0].ToString());
        Assert.Equal("192.168.1.5", rangeFull[4].ToString());

        // 3. Short range: 192.168.1.10-15
        var rangeShort = ClassroomManager.ParseIpInput("192.168.1.10-15");
        Assert.Equal(6, rangeShort.Count);
        Assert.Equal("192.168.1.10", rangeShort[0].ToString());
        Assert.Equal("192.168.1.15", rangeShort[5].ToString());

        // 4. CIDR notation /24
        var cidr = ClassroomManager.ParseIpInput("192.168.1.0/24");
        Assert.Equal(254, cidr.Count);
        Assert.Equal("192.168.1.1", cidr[0].ToString());
        Assert.Equal("192.168.1.254", cidr[253].ToString());
    }

    [Fact]
    public void TeacherDiscoveryService_GetAllBroadcastAddresses_ReturnsValidAddresses()
    {
        var addresses = LabManager.Network.Discovery.TeacherDiscoveryService.GetAllBroadcastAddresses();
        Assert.NotEmpty(addresses);
        Assert.Contains(System.Net.IPAddress.Broadcast, addresses);
    }
}
