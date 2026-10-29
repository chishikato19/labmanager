using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;

namespace LabManager.Teacher.Core.Services;

public class TeacherFileService : IDisposable
{
    private readonly TeacherCommandSender _sender = new();
    private readonly TcpCommandListener _fileListener = new();
    private string _currentCollectionFolder = string.Empty;
    private string _currentSessionTitle = "Nộp bài cho Giáo viên";
    private bool _isCollecting;
    private bool _prefixMachineName;
    private bool _separateMachineFolder;
    private bool _isDisposed;

    public event Action<FileCollectResponsePacket, string>? OnFileCollected;

    public string CurrentCollectionFolder => _currentCollectionFolder;
    public string CurrentSessionTitle => _currentSessionTitle;
    public bool IsCollecting => _isCollecting;
    public bool PrefixMachineName => _prefixMachineName;
    public bool SeparateMachineFolder => _separateMachineFolder;

    public TeacherFileService()
    {
        _fileListener.OnCommandReceived += HandleFileSubmissionAsync;
    }

    public void StartFileReceiver(int port = NetworkConfig.DefaultFilePort)
    {
        if (!_fileListener.IsListening)
        {
            _fileListener.Start(port);
        }
    }

    public async Task<int> DistributeFileAsync(IEnumerable<string> targetIps, string filePath, string targetSubFolder = "BaiTap", bool autoExtract = true)
    {
        if (!File.Exists(filePath)) return 0;

        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
        string fileName = Path.GetFileName(filePath);

        var packet = new FileDistributePacket
        {
            FileName = fileName,
            TargetSubFolder = targetSubFolder,
            FileBytes = fileBytes,
            AutoExtract = autoExtract
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.FileDistribute, packet);
        var results = await _sender.SendToMultipleAsync(targetIps, packetBytes, NetworkConfig.DefaultCommandPort);

        return results.Values.Count(success => success);
    }

    public async Task StartCollectionSessionAsync(
        IEnumerable<string> targetIps,
        string teacherIp,
        string folderPath,
        string sessionTitle = "Nộp bài cho Giáo viên",
        bool prefixMachineName = false,
        bool separateMachineFolder = false)
    {
        _currentCollectionFolder = folderPath;
        _currentSessionTitle = sessionTitle;
        _prefixMachineName = prefixMachineName;
        _separateMachineFolder = separateMachineFolder;
        _isCollecting = true;

        if (!Directory.Exists(_currentCollectionFolder))
        {
            Directory.CreateDirectory(_currentCollectionFolder);
        }

        StartFileReceiver(NetworkConfig.DefaultFilePort);

        var request = new FileCollectRequestPacket
        {
            TeacherIp = teacherIp,
            TeacherFilePort = NetworkConfig.DefaultFilePort,
            IsOpen = true,
            SessionTitle = sessionTitle,
            Instructions = "Vui lòng chọn tệp bài làm và nhấn Nộp bài."
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.FileCollectRequest, request);
        await _sender.SendToMultipleAsync(targetIps, packetBytes, NetworkConfig.DefaultCommandPort);
    }

    public async Task StopCollectionSessionAsync(IEnumerable<string> targetIps, string teacherIp)
    {
        _isCollecting = false;

        var request = new FileCollectRequestPacket
        {
            TeacherIp = teacherIp,
            TeacherFilePort = NetworkConfig.DefaultFilePort,
            IsOpen = false
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.FileCollectRequest, request);
        await _sender.SendToMultipleAsync(targetIps, packetBytes, NetworkConfig.DefaultCommandPort);
    }

    public async Task RequestCollectAllAsync(IEnumerable<string> targetIps, string teacherIp, string sourceSubFolder = "BaiTap")
    {
        // Tạo thư mục thu bài theo mốc thời gian nếu chưa có
        if (string.IsNullOrEmpty(_currentCollectionFolder) || !_isCollecting)
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folderName = $"ThuBai_{DateTime.Now:yyyyMMdd_HHmmss}";
            _currentCollectionFolder = Path.Combine(docs, "LabManager", folderName);
            Directory.CreateDirectory(_currentCollectionFolder);
        }

        StartFileReceiver(NetworkConfig.DefaultFilePort);

        var request = new FileCollectRequestPacket
        {
            TeacherIp = teacherIp,
            TeacherFilePort = NetworkConfig.DefaultFilePort,
            SourceSubFolder = sourceSubFolder,
            IsOpen = true,
            AutoCollectDesktop = true
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.FileCollectRequest, request);
        await _sender.SendToMultipleAsync(targetIps, packetBytes, NetworkConfig.DefaultCommandPort);
    }

    private async Task HandleFileSubmissionAsync(PacketHeader header, byte[] payload)
    {
        if (header.Type == CommandType.FileCollectResponse)
        {
            var submission = PacketSerializer.DeserializePayload<FileCollectResponsePacket>(payload, 0, payload.Length);
            if (submission != null && submission.ZipBytes.Length > 0)
            {
                if (string.IsNullOrEmpty(_currentCollectionFolder))
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _currentCollectionFolder = Path.Combine(docs, "LabManager", "ThuBai_MacDinh");
                    Directory.CreateDirectory(_currentCollectionFolder);
                }

                // Tên file gốc (không kèm tên học sinh)
                string rawName = !string.IsNullOrWhiteSpace(submission.OriginalFileName)
                    ? submission.OriginalFileName
                    : submission.ZipFileName;

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    rawName = "BaiNop";
                }

                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    rawName = rawName.Replace(c, '_');
                }

                // Tùy chọn 1: Kèm tên máy vào trước tên tệp (mặc định: Không)
                string finalFileName = _prefixMachineName && !string.IsNullOrWhiteSpace(submission.MachineName)
                    ? $"{submission.MachineName}_{rawName}"
                    : rawName;

                // Tùy chọn 2: Tách mỗi máy vào một thư mục riêng (mặc định: Không)
                string targetDir = _currentCollectionFolder;
                if (_separateMachineFolder && !string.IsNullOrWhiteSpace(submission.MachineName))
                {
                    string safeMachineDir = submission.MachineName;
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        safeMachineDir = safeMachineDir.Replace(c, '_');
                    }
                    targetDir = Path.Combine(_currentCollectionFolder, safeMachineDir);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                }

                string savePath = Path.Combine(targetDir, finalFileName);
                await File.WriteAllBytesAsync(savePath, submission.ZipBytes);

                OnFileCollected?.Invoke(submission, savePath);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _fileListener.Dispose();
    }
}
