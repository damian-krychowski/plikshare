using FluentAssertions;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class EncryptionBytesTests
{
    [Theory]
    [InlineData(0, 2)]      //header size + index
    [InlineData(5, 7)]      //header size + index
    [InlineData(7, 9)]      //header size + index
    [InlineData(8, 12)]     //header size + index + tagsize
    [InlineData(25, 31)]    //header size + index + 2x tagsize
    public void test_calculator_find_encrypted_index(long unencryptedIndex, long encryptedIndex)
    {
        //given
        var calculator = new EncryptedBytesRangeCalculator(
            headerSize: 2,
            firstSegmentCiphertextSize: 8,
            nextSegmentsCiphertextSize: 10,
            tagSize: 2);

        //when
        var actualEncryptedIndex = calculator.FindEncryptedIndex(unencryptedIndex);

        //then
        actualEncryptedIndex.Should().Be(encryptedIndex);
    }
}