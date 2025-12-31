using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace McpHealthServer.Services
{
    public class Session
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public string SseStreamUrl { get; set; } = string.Empty;
        public bool IsHandshakeCompleted { get; set; }
        public ConcurrentQueue<string> MessageQueue { get; } = new();
        public CancellationTokenSource CancellationTokenSource { get; } = new();
    }

    public interface ISessionService
    {
        Session CreateSession();
        Session? GetSession(string sessionId);
        bool RemoveSession(string sessionId);
        void CleanupExpiredSessions();
        IEnumerable<Session> GetAllSessions();
        void UpdateLastAccessed(string sessionId);
    }

    public class SessionService : ISessionService, IDisposable
    {
        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private readonly ILogger<SessionService> _logger;
        private readonly Timer _cleanupTimer;
        private readonly SessionOptions _options;

        public SessionService(IOptions<SessionOptions> options, ILogger<SessionService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _cleanupTimer = new Timer(CleanupExpiredSessionsCallback, null,
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));
        }

        public Session CreateSession()
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new Session
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                SseStreamUrl = $"/mcp/sse/{sessionId}",
                IsHandshakeCompleted = false
            };

            _sessions.TryAdd(sessionId, session);
            _logger.LogInformation("Created new session: {SessionId}", sessionId);

            return session;
        }

        public Session? GetSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return session;
            }
            return null;
        }

        public bool RemoveSession(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.CancellationTokenSource.Cancel();
                session.CancellationTokenSource.Dispose();
                _logger.LogInformation("Removed session: {SessionId}", sessionId);
                return true;
            }
            return false;
        }

        public void CleanupExpiredSessions()
        {
            var expiredThreshold = DateTime.UtcNow.AddMinutes(-_options.SessionTimeoutMinutes);
            var expiredSessions = _sessions
                .Where(kv => kv.Value.LastAccessedAt < expiredThreshold)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                RemoveSession(sessionId);
            }

            if (expiredSessions.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
            }
        }

        private void CleanupExpiredSessionsCallback(object? state)
        {
            CleanupExpiredSessions();
        }

        public IEnumerable<Session> GetAllSessions()
        {
            return _sessions.Values;
        }

        public void UpdateLastAccessed(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();

            foreach (var session in _sessions.Values)
            {
                session.CancellationTokenSource.Dispose();
            }
        }
    }

    public class SessionOptions
    {
        public int SessionTimeoutMinutes { get; set; } = 30;
        public int CleanupIntervalMinutes { get; set; } = 5;
    }
}