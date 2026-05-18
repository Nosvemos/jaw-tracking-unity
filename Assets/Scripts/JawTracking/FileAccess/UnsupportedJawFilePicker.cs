using System.Threading;
using System.Threading.Tasks;

namespace JawTracking.FileAccess
{
    public sealed class UnsupportedJawFilePicker : IJawFilePicker
    {
        public Task<JawFilePickResult> PickStlFileAsync(JawModelRole role, CancellationToken cancellationToken)
        {
            return Task.FromResult(JawFilePickResult.Failure("Bu platform için STL dosya seçici desteklenmiyor."));
        }
    }
}
