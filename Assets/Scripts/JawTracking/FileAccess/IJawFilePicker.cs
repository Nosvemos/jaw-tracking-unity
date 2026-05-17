using System.Threading;
using System.Threading.Tasks;

namespace JawTracking.FileAccess
{
    public interface IJawFilePicker
    {
        Task<JawFilePickResult> PickStlFileAsync(JawModelRole role, CancellationToken cancellationToken);
    }
}
