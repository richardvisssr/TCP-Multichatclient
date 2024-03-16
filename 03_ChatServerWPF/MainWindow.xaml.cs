using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace _03_ChatServerWPF
{
    public partial class MainWindow : Window
    {
        TcpListener tcpListener;
        private List<TcpClient> connectedClients = new List<TcpClient>();
        private Dictionary<TcpClient, string> clientUsernames = new Dictionary<TcpClient, string>();

        private int bufferSize;
        private bool isListening = false;

        private int MAX_ITEMS = 10;
        public int MAX_PORT_NUMBER = 65535;

        public MainWindow()
        {
            InitializeComponent();
            txtMessage.KeyDown += TxtMessage_KeyDown;
        }

        private void AddMessage(string message)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (listChats.Items.Count >= MAX_ITEMS)
                    {
                        listChats.Items.RemoveAt(0);
                    }
                    listChats.Items.Add(message);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fout bij toevoegen van bericht");
            }
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                btnSend_Click(sender, e);
            }
        }

        private async Task BroadcastMessageAsync(string message, TcpClient senderClient = null)
        {
            try
            {
                foreach (var client in connectedClients)
                {
                    if (client != senderClient && client.Connected)
                    {
                        try
                        {
                            string username = senderClient != null ? clientUsernames[senderClient] : "Server"; 
                            string messageWithUsername = $"{username}: {message}"; 

                            byte[] buffer = Encoding.ASCII.GetBytes(messageWithUsername + "@"); 

                            NetworkStream stream = client.GetStream();
                            await stream.WriteAsync(buffer, 0, buffer.Length);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error broadcasting message to client {client.Client.RemoteEndPoint}");
                           
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error broadcasting message");
            }
        }



        private async Task ReceiveDataAsync(TcpClient client)
        {
            StringBuilder messageBuilder = new StringBuilder();
            NetworkStream stream = client.GetStream();

            try
            {
                string username = clientUsernames[client];

                while (true)
                {
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                        break;  

                    messageBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                    string receivedMessage = messageBuilder.ToString();

                    if (receivedMessage.EndsWith("@"))
                    {
                        if (receivedMessage.Contains("disconnected"))
                        {
                            RemoveClient(client); 
                            break; 
                        }

                        await BroadcastMessageAsync(receivedMessage, client);

                        receivedMessage = receivedMessage.Substring(0, receivedMessage.Length - 1);
                        AddMessage($"[{username}]: {receivedMessage}");

                        messageBuilder.Clear();
                    }

                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Error receiving message from client");
                RemoveClient(client); 
            }
        }


        private void UpdateUserList()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    listClients.Items.Clear();
                    foreach (var kvp in clientUsernames)
                    {
                        listClients.Items.Add(kvp.Value);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fout bij bijwerken van gebruikerslijst");
            }
        }

        private async Task AcceptClientsAsync()
        {
            try
            {
                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    connectedClients.Add(client); 

                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string username = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    AddMessage($"Client connected: {username}");

                  
                    clientUsernames.Add(client, username);

                    UpdateUserList();

                    _ = Task.Run(() => ReceiveDataAsync(client));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kan clients niet accepteren");
            }
        }

        private async Task StopServerAsync()
        {
            try
            {
                foreach (var client in connectedClients)
                {
                    if (client.Connected)
                    {
                        NetworkStream stream = client.GetStream();
                        byte[] byeMessage = Encoding.ASCII.GetBytes("Server Stopped@");
                        await stream.WriteAsync(byeMessage, 0, byeMessage.Length);
                        stream.Close();
                        client.Close();
                    }
                }
                connectedClients.Clear();

                tcpListener.Stop();

                SetButtonState(false);
                AddMessage("Server stopped");
                btnStartStop.Content = "Start";
                isListening = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping server");
            }
        }


        private async void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            int port;

            if (!ValidatePort(txtPort.Text, out port) || !ValidateBufferSize(txtBufferSize.Text, out bufferSize))
            {
                return;
            }

            if (!isListening)
            {
                try
                {
                    tcpListener = new TcpListener(IPAddress.Any, port);
                    tcpListener.Start();

                    AddMessage("Listening for clients...");

                    btnStartStop.Content = "Stop";
                    isListening = true;

                    await Task.Run(AcceptClientsAsync);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    MessageBox.Show("Error starting server: Address already in use.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting server");
                }
            }
            else
            {
                await StopServerAsync();
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = txtMessage.Text;
            

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Please enter a message to send.");
                return; 
            }

            message = txtServername.Text + message;

            if (isListening)
            {
                await BroadcastMessageAsync(message);
            }
            else
            {
                MessageBox.Show("Server is not running.");
            }

            txtMessage.Clear();
            txtMessage.Focus();
        }

        private bool ValidatePort(string portString, out int port)
        {
            if (!int.TryParse(portString, out port) || port <= 0 || port > MAX_PORT_NUMBER)
            {
                MessageBox.Show("Ongeldige poortnummer.");
                return false;
            }
            return true;
        }

        private bool ValidateBufferSize(string bufferSizeString, out int bufferSize)
        {
            if (!int.TryParse(bufferSizeString, out bufferSize) || bufferSize <= 0)
            {
                MessageBox.Show("Ongeldige buffergrootte.");
                return false;
            }
            return true;
        }

        private void SetButtonState(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                btnStartStop.IsEnabled = !isConnected;
                btnSend.IsEnabled = isConnected;
            });
        }

        private void RemoveClient(TcpClient client)
        {
            if (clientUsernames.TryGetValue(client, out string username))
            {
                string disconnectMessage = $"disconnected@";
                BroadcastMessageAsync(disconnectMessage, client).Wait(); 
                AddMessage($"Client disconnected: {username}");
                connectedClients.Remove(client); 
                clientUsernames.Remove(client); 

               

            }

           
            UpdateUserList();
        }

        private void listClients_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
