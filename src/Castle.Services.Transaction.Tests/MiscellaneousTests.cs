#region License
// Copyright 2004-2022 Castle Project - https://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace Castle.Services.Transaction.Tests
{
    using System.Threading;

    using NUnit.Framework;

    public class MiscellaneousTests
    {
        [Test]
        [Description("As we are working on the same directories? We don't want to run the tests concurrently.")]
        [Ignore("TODO: Thread.CurrentThread.GetApartmentState() = MTA???")]
        public void CheckSTA()
        {
            var state = Thread.CurrentThread.GetApartmentState();

            // TODO: This somehow appears to be MTA in test projects.
            Assert.That(state, Is.EqualTo(ApartmentState.STA));
        }
    }
}