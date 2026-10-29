using Xunit;

// Vô hiệu hóa chạy song song để tránh xung đột cổng UDP/TCP mạng nội bộ giữa các bài test
[assembly: CollectionBehavior(DisableTestParallelization = true)]
