using Grpc.Core;
using {{ ProjectName }}.Proto;
{% if persistence ~= 'None' %}
using Microsoft.EntityFrameworkCore;
using {{ ProjectName }}.Domain;
using {{ ProjectName }}.Resources;
{% endif %}

namespace {{ ProjectName }}.Services;

{% if persistence ~= 'None' %}
// CRUD over the persisted {{ EntityName }} scaffold entity (Domain/{{ EntityName }}.cs) — the round trip a black-box
// test can prove end-to-end. Replace {{ EntityName }} and these handlers as your real domain lands.
// The base class is qualified through the Proto namespace: the generated service container
// class {{ ProjectName }} shares its name with the root namespace, so the bare
// name resolves to the namespace instead of the class.
public class {{ EntityName }}ServiceImpl : Proto.{{ ProjectName }}.{{ ProjectName }}Base
{
    private readonly AppDbContext _db;

    public {{ EntityName }}ServiceImpl(AppDbContext db) => _db = db;

    public override async Task<{{ EntityName }}> Create{{ EntityName }}(
        Create{{ EntityName }}Request request, ServerCallContext context)
    {
        var item = new {{ EntityName }}Entity { Id = Guid.NewGuid(), DisplayName = request.DisplayName };
        _db.{{ EntityName }}s.Add(item);
        await _db.SaveChangesAsync(context.CancellationToken);
        return ToEntity(item);
    }

    public override async Task<{{ EntityName }}> Get{{ EntityName }}(
        Get{{ EntityName }}Request request, ServerCallContext context)
        => ToEntity(await Find(request.Id, context));

    public override async Task<List{{ EntityName }}sResponse> List{{ EntityName }}s(
        List{{ EntityName }}sRequest request, ServerCallContext context)
    {
        var response = new List{{ EntityName }}sResponse();
        var {{ entity_name }}s = await _db.{{ EntityName }}s.OrderBy(i => i.CreatedAt).ToListAsync(context.CancellationToken);
        response.Items.AddRange({{ entity_name }}s.Select(ToEntity));
        return response;
    }

    public override async Task<{{ EntityName }}> Update{{ EntityName }}(
        Update{{ EntityName }}Request request, ServerCallContext context)
    {
        var item = await Find(request.Id, context);
        item.DisplayName = request.DisplayName;
        await _db.SaveChangesAsync(context.CancellationToken);
        return ToEntity(item);
    }

    public override async Task<Delete{{ EntityName }}Response> Delete{{ EntityName }}(
        Delete{{ EntityName }}Request request, ServerCallContext context)
    {
        var item = await Find(request.Id, context);
        _db.{{ EntityName }}s.Remove(item);
        await _db.SaveChangesAsync(context.CancellationToken);
        return new Delete{{ EntityName }}Response();
    }

    private async Task<{{ EntityName }}Entity> Find(string id, ServerCallContext context)
    {
        if (!Guid.TryParse(id, out var parsed))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{id}' is not a valid id"));
        var item = await _db.{{ EntityName }}s.FindAsync(new object[] { parsed }, context.CancellationToken);
        if (item is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"no item with id '{id}'"));
        return item;
    }

    private static {{ EntityName }} ToEntity({{ EntityName }}Entity item) =>
        new() { Id = item.Id.ToString(), DisplayName = item.DisplayName };
}
{% else %}
// In-memory stub handlers — nothing is persisted. Select a persistence option to render the
// scaffold CRUD backed by a real database.
// The base class is qualified through the Proto namespace: the generated service container
// class {{ ProjectName }} shares its name with the root namespace, so the bare
// name resolves to the namespace instead of the class.
public class {{ EntityName }}ServiceImpl : Proto.{{ ProjectName }}.{{ ProjectName }}Base
{
    public override Task<{{ EntityName }}> Create{{ EntityName }}(
        Create{{ EntityName }}Request request, ServerCallContext context)
        => Task.FromResult(new {{ EntityName }}
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = request.DisplayName,
        });

    public override Task<{{ EntityName }}> Get{{ EntityName }}(
        Get{{ EntityName }}Request request, ServerCallContext context)
        => Task.FromResult(new {{ EntityName }} { Id = request.Id, DisplayName = "" });

    public override Task<List{{ EntityName }}sResponse> List{{ EntityName }}s(
        List{{ EntityName }}sRequest request, ServerCallContext context)
        => Task.FromResult(new List{{ EntityName }}sResponse());

    public override Task<{{ EntityName }}> Update{{ EntityName }}(
        Update{{ EntityName }}Request request, ServerCallContext context)
        => Task.FromResult(new {{ EntityName }} { Id = request.Id, DisplayName = request.DisplayName });

    public override Task<Delete{{ EntityName }}Response> Delete{{ EntityName }}(
        Delete{{ EntityName }}Request request, ServerCallContext context)
        => Task.FromResult(new Delete{{ EntityName }}Response());
}
{% endif %}
