using Xunit;

// Các test live UI điều khiển chuột thật và enumerate cửa sổ theo process.
// Chạy song song hai class sẽ khiến hai instance SampleApp tranh nhau input và
// UIA trả về E_FAIL. Bắt buộc chạy tuần tự.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
