using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CurlThin;
using CurlThin.Enums;
using CurlThin.SafeHandles;

namespace UMassDM.Utils
{
    public static class Utils
    {
        //Source: https://stackoverflow.com/a/22078975

        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }

        public static SafeSlistHandle AppendHeader(this SafeSlistHandle list, string key, string value)
        {
            return CurlNative.Slist.Append(list != null ? list : SafeSlistHandle.Null, string.Format("{0}: {1}", key, value));
        }

        public static HttpStatusCode StatusCode(this SafeEasyHandle easy)
        {
            CurlNative.Easy.GetInfo(easy, CURLINFO.RESPONSE_CODE, out int code);
            return (HttpStatusCode)code;
        }
    }
}
