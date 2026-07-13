using NUnit.Framework;
using WordFlow.Adventure.UI;

namespace WordFlow.Adventure.Tests
{
    public sealed class AuthEmailValidationTests
    {
        [Test]
        public void PlainEmail_IsValid()
        {
            Assert.IsTrue(AuthControllerBase.IsValidEmail("parent@example.com"));
        }

        [Test]
        public void MissingAt_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail("parent.example.com"));
        }

        [Test]
        public void MissingDot_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail("parent@examplecom"));
        }

        [Test]
        public void Empty_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail(""));
        }

        [Test]
        public void Null_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail(null));
        }
    }
}
