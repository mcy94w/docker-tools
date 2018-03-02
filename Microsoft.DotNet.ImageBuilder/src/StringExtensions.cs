// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class StringExtensions
    {
        public static int IndexOfNth(this string str, string value, int nth = 1)
        {
            if (nth <= 0)
            {
                throw new ArgumentException($"'{nameof(nth)}' must be greater than zero.");
            }

            int index = str.IndexOf(value);
            for (int i = 1; i < nth; i++)
            {
                if (index == -1)
                {
                    break;
                }

                index = str.IndexOf(value, index + 1);
            }

            return index;
        }
    }
}