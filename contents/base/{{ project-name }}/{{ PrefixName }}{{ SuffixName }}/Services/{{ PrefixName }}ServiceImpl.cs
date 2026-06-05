using Grpc.Core;
using {{ PrefixName }}{{ SuffixName }}.Proto;
{% if persistence ~= 'None' %}
using {{ PrefixName }}{{ SuffixName }}.Resources;
{% endif %}
namespace {{ PrefixName }}{{ SuffixName }}.Services;

public class {{ PrefixName }}ServiceImpl : {{ PrefixName }}.{{ PrefixName }}Base
{
{% if persistence ~= 'None' %}
    private readonly AppDbContext _db;

    public {{ PrefixName }}ServiceImpl(AppDbContext db) => _db = db;
{% endif %}
    public override Task<{{ PrefixName }}Entity> Create{{ PrefixName }}(
        Create{{ PrefixName }}Request request, ServerCallContext context)
        => Task.FromResult(new {{ PrefixName }}Entity
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = request.DisplayName,
        });

    public override Task<{{ PrefixName }}Entity> Get{{ PrefixName }}(
        Get{{ PrefixName }}Request request, ServerCallContext context)
        => Task.FromResult(new {{ PrefixName }}Entity { Id = request.Id, DisplayName = "" });

    public override Task<List{{ PrefixName }}sResponse> List{{ PrefixName }}s(
        List{{ PrefixName }}sRequest request, ServerCallContext context)
        => Task.FromResult(new List{{ PrefixName }}sResponse());

    public override Task<{{ PrefixName }}Entity> Update{{ PrefixName }}(
        Update{{ PrefixName }}Request request, ServerCallContext context)
        => Task.FromResult(new {{ PrefixName }}Entity
        {
            Id = request.Id,
            DisplayName = request.DisplayName,
        });
}
