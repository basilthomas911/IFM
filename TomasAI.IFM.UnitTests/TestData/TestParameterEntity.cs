using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.UnitTests.TestData
{
    public class TestParameterEntity
    {
        private string _name;
        private int _age;

        public string Name => _name;
        public int Age => _age;

        public TestParameterEntity(string name, int age)
        {
            _name = name;
            _age = age;
        }

    }
}
