--- Acceptance suite for the .NET gRPC service archetype: renders each persistence variant,
--- verifies the layout, builds it, boots it against a real database container, and proves gRPC
--- CRUD calls round-trip into that database via prova's reflection-based dynamic client.
---
--- Run from the archetype repo root (uses ./prova.toml):   prova
--- requires docker + dotnet (SDK 9); skips cleanly without them.

local postgres = require("postgres")
local mysql    = require("mysql")

local SRC = "."

local BASE_ANSWERS = {
  author_name     = "Test Author",
  author_email    = "test@example.com",
  org_name        = "acme",
  solution_name   = "platform",
  prefix_name     = "Example",
  suffix_name     = "Service",
  image_registry  = "ghcr.io/acme",
}

local function answers_with(extra)
  local out = {}
  for k, v in pairs(BASE_ANSWERS) do out[k] = v end
  for k, v in pairs(extra) do out[k] = v end
  return out
end

-- prefix Example / suffix Service → proto package example_service, service Example.
local SVC = "example_service.Example"

local SCAFFOLD_FILES = {
  "ExampleService/Resources/Persistence.cs",
  "ExampleService/Resources/Persistence.Entities.cs",
  "ExampleService/Domain/Item.cs",
}

local VARIANTS = {
  {
    persistence = "PostgreSQL",
    db = postgres,
    count_by_name = [[SELECT count(*) FROM "Items" WHERE "DisplayName" = $1]],
  },
  {
    persistence = "MySQL",
    db = mysql,
    count_by_name = "SELECT count(*) FROM `Items` WHERE `DisplayName` = ?",
  },
}

for _, v in ipairs(VARIANTS) do
  local label = "dotnet-grpc[" .. v.persistence .. "]"

  local project = prova.fixture(label .. ":project", Scope.File, function(ctx)
    return archetect.render{
      source = SRC,
      answers = answers_with{ persistence = v.persistence },
      destination = ctx:tempdir(),
      defaults = true,
    }
  end)

  archetect.verify(project, {
    name = label,
    project_dir = "example-service",
    expected_files = {
      "ExampleService.sln",
      "ExampleService/Program.cs",
      "ExampleService/Protos/example_service.proto",
      "ExampleService/Services/ExampleServiceImpl.cs",
      SCAFFOLD_FILES[1], SCAFFOLD_FILES[2], SCAFFOLD_FILES[3],
      ".github/workflows/build.yaml",
    },
    yaml_globs = { ".platform/kubernetes/**/*.yaml" },
    requires = { "dotnet" },
    build_steps = { "dotnet build ExampleService.sln" },
  })

  local service = prova.fixture(label .. ":service", Scope.File, function(ctx)
    local root = ctx:use(project):dir("example-service")
    local db = v.db.container(ctx)

    shell.run("dotnet build ExampleService.sln -c Release", {
      cwd = root.path, timeout = "600s", check = true,
    })

    local port, mgmt = net.free_port(), net.free_port()
    ctx:manage(shell.spawn("dotnet ExampleService.dll", {
      cwd = root.path .. "/ExampleService/bin/Release/net9.0",
      env = {
        Port           = port,
        ManagementPort = mgmt,
        DbHost         = db.host,
        DbPort         = db.port,
        DbUsername     = "prova",
        DbPassword     = "prova",
        DbDbname       = "prova",
      },
    }))

    local addr = "127.0.0.1:" .. port
    -- Reflection answering proves the whole chain: EnsureCreated succeeded against the DB
    -- before Kestrel started serving.
    grpc.wait_for(addr, { timeout = "60s" })
    return { addr = addr, db = db.client }
  end)

  prova.group(label .. " CRUD round-trip", { requires = { "docker", "dotnet" } }, function(g)
    g:test("created entities land in " .. v.persistence, function(t)
      local svc = t:use(service)
      local client = grpc.client(svc.addr)

      -- Create through the public API...
      local created = client:call(SVC .. "/CreateExample", { display_name = "widget" })
      t:expect(created.display_name):equals("widget")
      t:expect(created.id, "created id"):is_truthy()

      -- ...prove the row is in the actual database...
      t:expect(svc.db:query_value(v.count_by_name, { "widget" }), "rows in DB"):equals(1)

      -- ...and read it back through the API (the old stub echoed instead of reading).
      local fetched = client:call(SVC .. "/GetExample", { id = created.id })
      t:expect(fetched.display_name):equals("widget")

      local listed = client:call(SVC .. "/ListExamples", {})
      local found = false
      for _, e in ipairs(listed.items or {}) do
        if e.id == created.id then found = true end
      end
      t:expect(found, "created entity present in ListExamples"):is_true()
    end)

    g:test("updates and deletes round-trip into " .. v.persistence, function(t)
      local svc = t:use(service)
      local client = grpc.client(svc.addr)

      local created = client:call(SVC .. "/CreateExample", { display_name = "ephemeral" })

      local updated = client:call(SVC .. "/UpdateExample", { id = created.id, display_name = "renamed" })
      t:expect(updated.display_name):equals("renamed")
      t:expect(svc.db:query_value(v.count_by_name, { "renamed" }), "renamed row in DB"):equals(1)
      t:expect(svc.db:query_value(v.count_by_name, { "ephemeral" }), "old name gone"):equals(0)

      client:call(SVC .. "/DeleteExample", { id = created.id })
      local gone = client:call_status(SVC .. "/GetExample", { id = created.id })
      t:expect(gone.code):equals("NotFound")
      t:expect(svc.db:query_value(v.count_by_name, { "renamed" }), "row deleted from DB"):equals(0)
    end)

    g:test("gRPC health service reports SERVING", function(t)
      local svc = t:use(service)
      local client = grpc.client(svc.addr)
      local health = client:call("grpc.health.v1.Health/Check", {})
      t:expect(health.status):equals("SERVING")
    end)
  end)
end

-- The hollow rendering stays hollow: stub handlers, no scaffold files.
archetect.verify{
  name = "dotnet-grpc[None]",
  source = SRC,
  answers = answers_with{ persistence = "None" },
  project_dir = "example-service",
  expected_files = {
    "ExampleService.sln",
    "ExampleService/Program.cs",
    "ExampleService/Protos/example_service.proto",
  },
  absent_files = SCAFFOLD_FILES,
  requires = { "dotnet" },
  build_steps = { "dotnet build ExampleService.sln" },
}
