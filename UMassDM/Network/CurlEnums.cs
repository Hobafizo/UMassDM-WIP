using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UMassDM.Network
{
    public enum SSLVersion : int
    {
        CURL_SSLVERSION_DEFAULT,
        CURL_SSLVERSION_TLSv1,
        CURL_SSLVERSION_SSLv2,
        CURL_SSLVERSION_SSLv3,
        CURL_SSLVERSION_TLSv1_0,
        CURL_SSLVERSION_TLSv1_1,
        CURL_SSLVERSION_TLSv1_2,
        CURL_SSLVERSION_TLSv1_3
    }
}
