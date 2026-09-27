using System;

namespace Zpd.Networking
{
    /// <summary>Owns the current account credentials in memory. No tokens are persisted.</summary>
    public sealed class TokenStorage
    {
        public AccountSession Current { get; private set; }
        public int Revision { get; private set; }
        public bool HasSession => Current != null && !Current.IsExpired;

        internal event Action Changed;

        public bool IsCurrent(AccountSession session)
        {
            return session != null && ReferenceEquals(Current, session) && !session.IsExpired;
        }

        internal void Replace(AccountSession session)
        {
            if (session == null || session.IsExpired)
            {
                throw new ArgumentException("A valid authenticated session is required.", nameof(session));
            }

            Current = session;
            Revision++;
            Changed?.Invoke();
        }

        internal void Clear()
        {
            Current = null;
            Revision++;
            Changed?.Invoke();
        }

        internal void Invalidate(AccountSession expected)
        {
            if (expected != null && ReferenceEquals(Current, expected))
            {
                Clear();
            }
        }
    }
}
