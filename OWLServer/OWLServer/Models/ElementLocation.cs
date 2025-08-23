namespace OWLServer.Models
{
    public class ElementLocation
    {
        public int Id { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        
        public int offsetTop { get; set; }
        public int offsetLeft { get; set; }
        public double Top { get; set; }
        public double Left { get; set; }

        public string ToLocationString()
        {
            return $"position:absolute;" +
                   $"top:calc(100% * {Top.ToString().Replace(',', '.')} - 1.5rem);" +
                   $"left:calc(100% * {Left.ToString().Replace(',', '.')} - 1.5rem);";
        }
    }
}
