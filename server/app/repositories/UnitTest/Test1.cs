using System.Collections.Generic;

namespace UnitTest
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void Repository_Methods_Compile()
        {
            var repo = new RepositoriesCore.EmployeesRepository("Server=.;Database=Dummy;User Id=sa;Password=pwd;");
            var initResult = repo.InitializeDatabase(new Dictionary<string,string>());
            var isInitialized = repo.DatabaseIsInitialized();
            // Empty implementation expectations (both should be false / default)
            Assert.IsFalse(initResult);
            Assert.IsFalse(isInitialized);
        }
    }
}
