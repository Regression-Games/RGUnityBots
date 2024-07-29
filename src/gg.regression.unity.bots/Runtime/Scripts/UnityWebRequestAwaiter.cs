using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace RegressionGames
{
    public readonly struct UnityWebRequestAwaiter : INotifyCompletion
    {
        private readonly UnityWebRequestAsyncOperation _asyncOperation;


        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOperation) => _asyncOperation = asyncOperation;

        public UnityWebRequestAwaiter GetAwaiter()
        {
            return this;
        }

        public UnityWebRequest GetResult() => _asyncOperation.webRequest;

        public void OnCompleted(Action continuation) => _asyncOperation.completed += _ => continuation();

        public bool IsCompleted => _asyncOperation.isDone;
    }
}
