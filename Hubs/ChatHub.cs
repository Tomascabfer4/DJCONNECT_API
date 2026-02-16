using Microsoft.AspNetCore.SignalR;

namespace API_DJCONNECT.Hubs;

public class ChatHub : Hub
{
    // El Frontend llamará a esto: connection.invoke("UnirseAlGrupo", "15")
    public async Task UnirseAlGrupo(string reservaId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, reservaId);
    }

    // Opcional: Para salir del chat
    public async Task SalirDelGrupo(string reservaId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, reservaId);
    }
}