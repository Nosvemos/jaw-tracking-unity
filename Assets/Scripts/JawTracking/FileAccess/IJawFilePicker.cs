using System.Threading;
using System.Threading.Tasks;

namespace JawTracking.FileAccess
{
    public interface IJawFilePicker
    {
        Task<JawFilePickResult> PickModelFileAsync(JawModelRole role, CancellationToken cancellationToken);
    }
}
