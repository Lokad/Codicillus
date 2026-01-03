using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Abstractions;

public interface IModelAdapter
{
    ModelCapabilities Capabilities { get; }

    IAsyncEnumerable<ResponseEvent> StreamAsync(ModelPrompt prompt, CancellationToken cancellationToken);

    Task<IReadOnlyList<ResponseItem>> CompactAsync(ModelPrompt prompt, CancellationToken cancellationToken);
}
