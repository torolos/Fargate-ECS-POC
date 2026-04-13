namespace dotnetAPI.tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        // Arrange
        Assert.Equal(4, 2 + 2);
    }

    [Fact]
    public void Test2()
    {
        // Arrange
        Assert.NotEqual(5, 2 + 2);
    }

    [Fact]
    public void Test3()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act
        list.Add(4);

        // Assert
        Assert.Equal(4, list.Count);
    
    }

}
