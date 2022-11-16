using Services.Interfaces;

namespace Services.Implementation
{
    public class UniqueCreator : IUniqueCreator
    {
        public string GetCurentDateTimeString() => DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");
    }
}