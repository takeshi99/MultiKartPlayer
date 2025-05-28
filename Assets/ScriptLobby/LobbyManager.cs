using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor.PackageManager;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine.SceneManagement;
public class LobbyManager : MonoBehaviour
{
    public GameObject botaoInicia;
    public TMP_Text[] lobbyList; 
    public TextMeshProUGUI TextoCodigo;
    public TMP_InputField inputFieldInseriCodigo, InputNome;
    public bool iniciouOJogo;
    Lobby hostLobby, joinedLobby;

    // Start is called before the first frame update
    async void Start()
    {
        await UnityServices.InitializeAsync();
        await LogaAnonimamente();
      

    }

    async Task LogaAnonimamente()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            return;
        }
        //limpa o ip  da sessao, para que o mesmo ip crie contas anonimas diferentes 
        //so para o parrale sync
        AuthenticationService.Instance.ClearSessionToken();

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

      
    }
    public async Task CriaSala()
    {

        try
        {
            CreateLobbyOptions LobbyName = new CreateLobbyOptions
            {
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { "StarGame",new DataObject(DataObject.VisibilityOptions.Public,"0")}
                }
            };

            hostLobby = await LobbyService.Instance.CreateLobbyAsync("Nome", 20, LobbyName);

            joinedLobby = hostLobby;

            TextoCodigo.text = hostLobby.LobbyCode;

            //fica chamando a cada sec 

            InvokeRepeating("enviaPing", 5, 10);

            InvokeRepeating("verificaUpdate", 3, 3);

            if (hostLobby != null) 
            {
                botaoInicia.SetActive(true);
            }



        } 
        catch(LobbyServiceException e )
        {
            Debug.LogException(e);
            if (e.ErrorCode == 16004) 
            {

                Debug.Log("O lobby esta cheio!");
            }

        }
    }
    public void MostraPlayers()
    {
        if (joinedLobby == null || joinedLobby.Players == null) 
        return ;

        for (int i = 0; i < joinedLobby.Players.Count; i++) 
        {
            lobbyList[i].text = joinedLobby.Players[i].Data["nome"].Value;
        }
        
    }
    public void verificaUpdate() 
    {
        if (joinedLobby == null)
            return;
        if (joinedLobby.Data!=null && joinedLobby.Data.ContainsKey("StarGame") && joinedLobby.Data["StarGame"].Value !="0") 
        {
            Debug.Log("Entrou aq");
            entraRelay(joinedLobby.Data["StarGame"].Value);
        }
        AtualizaLobby();
        MostraPlayers();

        // relizar conexao relay
    }
    public async void AtualizaLobby() 
    {
        if (joinedLobby == null )
        return;

        joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

    }
    
    

    public async void BotaoCria() 
    {
        await CriaSala();
    }
    public async void BotaoEntrar()  
    {
        await EntrarSala();
    }

    Player GetPlayer() 
    {
        Player player = new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "nome", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public,InputNome.text)}
            }

        };
        return player;
    }

    public async Task EntrarSala() 
    {

        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                JoinLobbyByCodeOptions lobbyOptions = new JoinLobbyByCodeOptions
                {
                    Player = GetPlayer()
                };

                joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(inputFieldInseriCodigo.text, lobbyOptions);
                //linha abaixo caso queira que o client veja o codigo da sala 
                TextoCodigo.text = joinedLobby.LobbyCode;

                InvokeRepeating("verificaUpdate",3,3);



            }

        }
        catch (LobbyServiceException e) 
        {
        
            Debug.LogException(e);
            if (e.ErrorCode == 16010) 
            {
                Debug.Log("Codigo Invalido!");
            }
        }
     
    }
    public async void enviaPing() 
    {
        if (hostLobby!=null) 
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            Debug.Log("Enviou Ping");
        }

    }
    async Task<string> CriaRelay() 
    {

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(20);
       
        string codigoAloc = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        RelayServerData relayServerData = new RelayServerData(allocation,"dtls");

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartHost();

        return codigoAloc;
    }

    async void entraRelay(string codigoDeAlocacao) 
    {
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(codigoDeAlocacao);
        
        RelayServerData relayServerData = new RelayServerData(joinAllocation,"dtls");

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData); 

        NetworkManager.Singleton.StartClient();

        CancelInvoke("verificaUpdate");
    }

    public async void InicaJogo() 
    {
        if (joinedLobby == null)
            return;

        string CodigoRelay = await CriaRelay();

      
        Lobby lobby = await Lobbies.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject> 
            {
                {"StarGame", new DataObject(DataObject.VisibilityOptions.Public,CodigoRelay) }
            }
        });
        joinedLobby = lobby;
        //CancelInvoke("verificaUpdate");
        CancelInvoke("enviaPing");

        iniciouOJogo = true;
        NetworkManager.Singleton.SceneManager.LoadScene("MainScene",LoadSceneMode.Single);
      
        
    }

}
