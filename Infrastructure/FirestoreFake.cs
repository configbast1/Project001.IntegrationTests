using System.Collections.Concurrent;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore;
using V1 = Google.Cloud.Firestore.V1;
using Wkt = Google.Protobuf.WellKnownTypes;

namespace Project001.IntegrationTests.Infrastructure;

/// <summary>
/// Настоящий FirestoreDb поверх замоканного gRPC-клиента.
/// Сеть не используется; все записи (Commit) складываются в Commits.
/// </summary>
public sealed class FirestoreFake
{
    public Mock<V1.FirestoreClient> Client { get; } = new();
    public ConcurrentQueue<V1.CommitRequest> Commits { get; } = new();
    public FirestoreDb Db { get; }

    /// <summary>Если true, каждый Commit падает с исключением.</summary>
    public bool FailCommits { get; set; }

    public FirestoreFake()
    {
        Setup();
        Db = FirestoreDb.Create("test-project", Client.Object);
    }

    public void Reset()
    {
        Client.Reset();
        Commits.Clear();
        FailCommits = false;
        Setup();
    }

    private void Setup()
    {
        Client.Setup(c => c.CommitAsync(It.IsAny<V1.CommitRequest>(), It.IsAny<CallSettings>()))
            .Returns<V1.CommitRequest, CallSettings>((request, _) =>
            {
                if (FailCommits)
                {
                    return Task.FromException<V1.CommitResponse>(new InvalidOperationException("firestore unavailable"));
                }

                Commits.Enqueue(request);
                return Task.FromResult(new V1.CommitResponse
                {
                    CommitTime = Wkt.Timestamp.FromDateTime(DateTime.UtcNow),
                    WriteResults = { request.Writes.Select(_ => new V1.WriteResult()) }
                });
            });
    }

    /// <summary>Единственная запись единственного коммита.</summary>
    public V1.Write SingleWrite()
    {
        var commit = Assert.Single(Commits);
        return Assert.Single(commit.Writes);
    }
}
