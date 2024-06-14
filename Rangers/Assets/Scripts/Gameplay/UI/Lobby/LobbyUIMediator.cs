using BTG.ConnectionManagement;
using BTG.UnityServices.Auth;
using BTG.UnityServices.Lobbies;
using BTG.Utilities;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using VContainer;

namespace BTG.Gameplay.UI
{
    public class  LobbyUIMediator : MonoBehaviour
    {
        private const string DEFAULT_LOBBY_NAME = "no-name";

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private LobbyJoiningUI _lobbyJoiningUI;
        [SerializeField] private LobbyCreationUI _lobbyCreationUI;
        [SerializeField] private UITinter _joinToggleHighlight;
        [SerializeField] private UITinter _joinToggleTabBlocker;
        [SerializeField] private UITinter _createToggleHighlight;
        [SerializeField] private UITinter _createToggleTabBlocker;
        [SerializeField] private TextMeshProUGUI _playerNameLabel;
        [SerializeField] private GameObject _loadingSpinner;

        private AuthenticationServiceFacade _authenticationServiceFacade;
        private LobbyServiceFacade _lobbyServiceFacade;
        private LocalLobbyUser _localLobbyUser;
        private LocalLobby _localLobby;

        private ConnectionManager _connectionManager;
        ISubscriber<ConnectStatus> _connectStatusSubscriber;

        private void Start()
        {
            _lobbyCreationUI.Hide();
            _lobbyJoiningUI.Hide();
        }

        private void OnDestroy()
        {
            if (_connectStatusSubscriber != null)
            {
                _connectStatusSubscriber.Unsubscribe(OnConnectStatusChanged);
            }
        }

        [Inject]
        private void InjectDependenciesAndInitialize(
            AuthenticationServiceFacade authenticationServiceFacade,
            LobbyServiceFacade lobbyServiceFacade,
            LocalLobbyUser localLobbyUser,
            LocalLobby localLobby,
            ISubscriber<ConnectStatus> connectStatusSubscriber,
            ConnectionManager connectionManager)
        {
            _authenticationServiceFacade = authenticationServiceFacade;
            _lobbyServiceFacade = lobbyServiceFacade;
            _localLobbyUser = localLobbyUser;
            _localLobby = localLobby;

            _connectStatusSubscriber = connectStatusSubscriber;
            _connectionManager = connectionManager;

            _connectStatusSubscriber.Subscribe(OnConnectStatusChanged);
        }


        /// <summary>
        /// Creates a new lobby with a given name and privacy setting. 
        /// If the player is authorized, it attempts to create the lobby and sets the local user as the host if successful. 
        /// The UI is blocked during the process and unblocked afterwards, regardless of success or failure.
        /// </summary>
        public async void CreateLobbyRequest(string lobbyName, bool isPrivate)
        {
            // before sending request to lobby service, populate an empty lobby name, if necessary
            if (string.IsNullOrEmpty(lobbyName))
            {
                lobbyName = DEFAULT_LOBBY_NAME;
            }

            BlockUIWhileLoadingIsInProgress();

            if (!_authenticationServiceFacade.IsAuthorizedToAuthenticationService())
            {
                UnblockUIAfterLoadingIsComplete();
                return;
            }

            (bool Success, Lobby Lobby) lobbyCreationAttempt = 
                await _lobbyServiceFacade.TryCreateLobbyAsync(lobbyName, _connectionManager.MaxConnectedPlayers, isPrivate);

            if (lobbyCreationAttempt.Success)
            {
                _localLobbyUser.IsHost = true;
                _lobbyServiceFacade.SetRemoteLobby(lobbyCreationAttempt.Lobby);

                Debug.Log($"Created lobby with ID: {_localLobby.LobbyID} and code {_localLobby.LobbyCode}");
                _connectionManager.StartHostLobby(_localLobbyUser.PlayerName);
            }
            else
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        /// <summary>
        /// an asynchronous method that retrieves and publishes a list of lobbies. 
        /// If the blockUI parameter is true, it blocks the user interface during the process. 
        /// It first checks if the Unity Services are initialized, and if not, it returns. 
        /// Then, it checks if the player is authorized. If the player is not authorized and blockUI is true, 
        /// it unblocks the user interface and returns. After retrieving and publishing the lobby list, 
        /// it unblocks the user interface if blockUI is true.
        /// </summary>
        public async void QueryLobbiesRequest(bool blockUI)
        {
            if (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized)
            {
                return;
            }

            if (blockUI)
            {
                BlockUIWhileLoadingIsInProgress();
            }

            if (blockUI && !_authenticationServiceFacade.IsAuthorizedToAuthenticationService())
            {
                UnblockUIAfterLoadingIsComplete();
                return;
            }

            await _lobbyServiceFacade.RetrieveAndPublishLobbyListAsync();

            if (blockUI)
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        /// <summary>
        /// an asynchronous method that attempts to join a lobby using a provided lobby code. 
        /// It blocks the user interface during the process. If the player is not authorized, 
        /// it unblocks the user interface and returns. If the attempt to join the lobby is successful, 
        /// it calls the OnJoinedLobby method. If the attempt is not successful, it unblocks the user interface.
        /// </summary>
        public async void JoinLobbyWithCodeRequest(string lobbyCode)
        {
            BlockUIWhileLoadingIsInProgress();

            if (!_authenticationServiceFacade.IsAuthorizedToAuthenticationService())
            {
                UnblockUIAfterLoadingIsComplete();
                return;
            }

            (bool Success, Lobby Lobby) lobbyJoinAttempt = await _lobbyServiceFacade.TryJoinLobbyAsync(null, lobbyCode);

            if (lobbyJoinAttempt.Success)
            {
                OnJoinedLobby(lobbyJoinAttempt.Lobby);
            }
            else
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        /// <summary>
        /// an asynchronous method that attempts to join a specific lobby identified by the localLobby parameter. 
        /// It blocks the user interface during the process. If the player is not authorized, 
        /// it unblocks the user interface and returns. If the attempt to join the lobby is successful, 
        /// it calls the OnJoinedLobby method. If the attempt is not successful, it unblocks the user interface.
        /// </summary>
        public async void JoinLobbyRequest(LocalLobby localLobby)
        {
            BlockUIWhileLoadingIsInProgress();

            if (!_authenticationServiceFacade.IsAuthorizedToAuthenticationService())
            {
                UnblockUIAfterLoadingIsComplete();
                return;
            }

            (bool Success, Lobby Lobby) lobbyJoinAttempt = await _lobbyServiceFacade.TryJoinLobbyAsync(localLobby.LobbyID, localLobby.LobbyCode);

            if (lobbyJoinAttempt.Success)
            {
                OnJoinedLobby(lobbyJoinAttempt.Lobby);
            }
            else
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        /// <summary>
        /// an asynchronous method that attempts to quickly join a lobby. 
        /// It blocks the user interface during the process. If the player is not authorized, 
        /// it unblocks the user interface and returns. If the attempt to join the lobby is successful, 
        /// it calls the OnJoinedLobby method. If the attempt is not successful, it unblocks the user interface.
        /// </summary>
        public async void QuickJoinRequest()
        {
            BlockUIWhileLoadingIsInProgress();

            if (!_authenticationServiceFacade.IsAuthorizedToAuthenticationService())
            {
                UnblockUIAfterLoadingIsComplete();
                return;
            }

            (bool Success, Lobby Lobby) lobbyJoinAttempt = await _lobbyServiceFacade.TryQuickJoinLobbyAsync();

            if (lobbyJoinAttempt.Success)
            {
                OnJoinedLobby(lobbyJoinAttempt.Lobby);
            }
            else
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        public void ToggleCreateLobbyUI()
        {
            _lobbyJoiningUI.Hide();
            _lobbyCreationUI.Show();
            _joinToggleHighlight.SetToColor(0);
            _joinToggleTabBlocker.SetToColor(0);
            _createToggleHighlight.SetToColor(1);
            _createToggleTabBlocker.SetToColor(1);
        }

        public void ToggleJoinLobbyUI()
        {
            _lobbyJoiningUI.Show();
            _lobbyCreationUI.Hide();
            _joinToggleHighlight.SetToColor(1);
            _joinToggleTabBlocker.SetToColor(1);
            _createToggleHighlight.SetToColor(0);
            _createToggleTabBlocker.SetToColor(0);
        }

        public void Show()
        {
            _canvasGroup.alpha = 1;
            _canvasGroup.blocksRaycasts = true;

            ShowName();
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;
            _lobbyCreationUI.Hide();
            _lobbyJoiningUI.Hide();
        }

        private void ShowName()
        {
            _playerNameLabel.text = _localLobbyUser.PlayerName;
        }

        private void OnConnectStatusChanged(ConnectStatus status)
        {
            if (status is ConnectStatus.GenericDisconnect or ConnectStatus.StartClientFailed)
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        private void BlockUIWhileLoadingIsInProgress()
        {
            _canvasGroup.interactable = false;
            _loadingSpinner.SetActive(true);
        }

        private void UnblockUIAfterLoadingIsComplete()
        {
            // this callback can happen after we've already switched to a different scene
            // in that case the canvas group would be null
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _loadingSpinner.SetActive(false);
            }
        }

        private void OnJoinedLobby(Lobby remoteLobby)
        {
            _lobbyServiceFacade.SetRemoteLobby(remoteLobby);
            Debug.Log($"Joined lobby with ID: {_localLobby.LobbyID} and Internal Relay join code {_localLobby.RelayJoinCode}");
            _connectionManager.StartClientLobby(_localLobbyUser.PlayerName);
        }
    }
}