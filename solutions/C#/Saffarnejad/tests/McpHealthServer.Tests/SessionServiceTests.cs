using McpHealthServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace McpHealthServer.Tests.Services
{
    public class SessionServiceTests
    {
        private readonly Mock<IOptions<SessionOptions>> _mockOptions;
        private readonly Mock<ILogger<SessionService>> _mockLogger;
        private readonly SessionService _service;

        public SessionServiceTests()
        {
            _mockOptions = new Mock<IOptions<SessionOptions>>();
            _mockOptions.Setup(o => o.Value)
                .Returns(new SessionOptions
                {
                    SessionTimeoutMinutes = 30,
                    CleanupIntervalMinutes = 5
                });

            _mockLogger = new Mock<ILogger<SessionService>>();
            _service = new SessionService(_mockOptions.Object, _mockLogger.Object);
        }

        [Fact]
        public void CreateSession_ShouldReturnNewSession()
        {
            // Act
            var session = _service.CreateSession();

            // Assert
            Assert.NotNull(session);
            Assert.NotEmpty(session.SessionId);
            Assert.False(session.IsHandshakeCompleted);
            Assert.Contains("sse", session.SseStreamUrl);
        }

        [Fact]
        public void GetSession_WithValidId_ShouldReturnSession()
        {
            // Arrange
            var createdSession = _service.CreateSession();

            // Act
            var retrievedSession = _service.GetSession(createdSession.SessionId);

            // Assert
            Assert.NotNull(retrievedSession);
            Assert.Equal(createdSession.SessionId, retrievedSession.SessionId);
        }

        [Fact]
        public void RemoveSession_ShouldRemoveSession()
        {
            // Arrange
            var session = _service.CreateSession();

            // Act
            var result = _service.RemoveSession(session.SessionId);

            // Assert
            Assert.True(result);
            Assert.Null(_service.GetSession(session.SessionId));
        }

        [Fact]
        public void CleanupExpiredSessions_ShouldRemoveOldSessions()
        {
            // Arrange
            var session = _service.CreateSession();

            // Simulate old session by modifying LastAccessedAt
            var sessionRef = _service.GetSession(session.SessionId);
            if (sessionRef != null)
            {
                sessionRef.LastAccessedAt = DateTime.UtcNow.AddHours(-1);
            }

            // Act
            _service.CleanupExpiredSessions();

            // Assert
            Assert.Null(_service.GetSession(session.SessionId));
        }
    }
}