using System.Drawing;
using OWLServer.Models;

namespace OWLServer.Services;

public static class Util
{
    public static Color TeamColorToColorTranslator(TeamColor color)
    {
        if (color == TeamColor.BLUE)
            return Color.Blue;
        if (color == TeamColor.RED)
            return Color.Red;
        if (color == TeamColor.OFF)
            return Color.Black;
        if (color == TeamColor.LOCKED)
            return Color.Yellow;

        return Color.White;
    }

    public static string HTMLColorForTeam(TeamColor color)
    {
        if (color == TeamColor.BLUE)
        {
            return "#00b4f1";
        }
        else if (color == TeamColor.RED)
        {
            return "#fc1911";
        }

        return "#ffffff";
    }
}