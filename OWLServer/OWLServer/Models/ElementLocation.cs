namespace OWLServer.Models
{
    public class ElementLocation
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Top { get; set; }
        public double Left { get; set; }

        public string ToLocationString()
        {
            return $"" +
                   $"top:calc(100% * {Top.ToString().Replace(',', '.')});" +
                   $"left:calc(100% * {Left.ToString().Replace(',', '.')});";
        }
    }
}
