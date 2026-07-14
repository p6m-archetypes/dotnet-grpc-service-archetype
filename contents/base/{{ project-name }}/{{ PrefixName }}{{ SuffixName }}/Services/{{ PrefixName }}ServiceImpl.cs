using Grpc.Core;
using {{ PrefixName }}{{ SuffixName }}.Proto;
{% if persistence ~= 'None' %}
using Microsoft.EntityFrameworkCore;
using {{ PrefixName }}{{ SuffixName }}.Domain;
using {{ PrefixName }}{{ SuffixName }}.Resources;
{% endif %}

namespace {{ PrefixName }}{{ SuffixName }}.Services;

{% if persistence ~= 'None' %}
// CRUD over the persisted Item scaffold entity (Domain/Item.cs) — the round trip a black-box
// test can prove end-to-end. Replace Item and these handlers as your real domain lands.
public class {{ PrefixName }}ServiceImpl : {{ PrefixName }}.{{ PrefixName }}Base
{
    private readonly AppDbContext _db;

    public {{ PrefixName }}ServiceImpl(AppDbContext db) => _db = db;

    public override async Task<{{ PrefixName }}Entity> Create{{ PrefixName }}(
        Create{{ PrefixName }}Request request, ServerCallContext context)
    {
        var item = new Item { Id = Guid.NewGuid(), DisplayName = request.DisplayName };
        _db.Items.Add(item);
        await _db.SaveChangesAsync(context.CancellationToken);
        return ToEntity(item);
    }

    public override async Task<{{ PrefixName }}Entity> Get{{ PrefixName }}(
        Get{{ PrefixName }}Request request, ServerCallContext context)
        => ToEntity(await Find(request.Id, context));

    public override async Task<List{{ PrefixName }}sResponse> List{{ PrefixName }}s(
        List{{ PrefixName }}sRequest request, ServerCallContext context)
    {
        var response = new List{{ PrefixName }}sResponse();
        var items = await _db.Items.OrderBy(i => i.CreatedAt).ToListAsync(context.CancellationToken);
        response.Items.AddRange(items.Select(ToEntity));
        return response;
    }

    public override async Task<{{ PrefixName }}Entity> Update{{ PrefixName }}(
        Update{{ PrefixName }}Request request, ServerCallContext context)
    {
        var item = await Find(request.Id, context);
        item.DisplayName = request.DisplayName;
        await _db.SaveChangesAsync(context.CancellationToken);
        return ToEntity(item);
    }

    public override async Task<Delete{{ PrefixName }}Response> Delete{{ PrefixName }}(
        Delete{{ PrefixName }}Request request, ServerCallContext context)
    {
        var item = await Find(request.Id, context);
        _db.Items.Remove(item);
        await _db.SaveChangesAsync(context.CancellationToken);
        return new Delete{{ PrefixName }}Response();
    }

    private async Task<Item> Find(string id, ServerCallContext context)
    {
        if (!Guid.TryParse(id, out var parsed))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{id}' is not a valid id"));
        var item = await _db.Items.FindAsync(new object[] { parsed }, context.CancellationToken);
        if (item is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"no item with id '{id}'"));
        return item;
    }

    private static {{ PrefixName }}Entity ToEntity(Item item) =>
        new() { Id = item.Id.ToString(), DisplayName = item.DisplayName };
}
{% else %}
// In-memory stub handlers — nothing is persisted. Select a persistence option to render the
// scaffold CRUD backed by a real database.
public class {{ PrefixName }}ServiceImpl : {{ PrefixName }}.{{ PrefixName }}Base
{
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
        => Task.FromResult(new {{ PrefixName }}Entity { Id = request.Id, DisplayName = request.DisplayName });

    public override Task<Delete{{ PrefixName }}Response> Delete{{ PrefixName }}(
        Delete{{ PrefixName }}Request request, ServerCallContext context)
        => Task.FromResult(new Delete{{ PrefixName }}Response());
}
{% endif %}
