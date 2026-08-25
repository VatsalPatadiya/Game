using System;

namespace GameDomain.Generation
{
    public sealed class BoardGenerationException : Exception
    {
        public BoardGenerationException(string message) : base(message) { }
    }
}
