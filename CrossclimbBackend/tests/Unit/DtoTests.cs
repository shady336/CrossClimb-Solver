using CrossclimbBackend.Models;
using Newtonsoft.Json;
using Xunit;

namespace CrossclimbBackend.UnitTests
{
    public class DtoTests
    {
        [Fact]
        public void SolveLadderRequest_SerializationRoundtrip()
        {
            var req = new SolveLadderRequest { WordLength = 5, Clues = { "Quick mind" } };
            var json = JsonConvert.SerializeObject(req);
            var back = JsonConvert.DeserializeObject<SolveLadderRequest>(json);
            Assert.NotNull(back);
            Assert.Equal(5, back.WordLength);
            Assert.Single(back.Clues);
        }
    }
}
