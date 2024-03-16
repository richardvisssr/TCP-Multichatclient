using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace _03_ChatClientWPF
{
    public partial class MainWindow : Window
    {
        TcpClient tcpClient;
        NetworkStream networkStream;
        private const int MAX_ITEMS = 10;

        private int bufferSize;
        private bool isConnected = false;
        private string username; 

        public MainWindow()
        {
            InitializeComponent();
            txtMessage.KeyDown += TxtMessage_KeyDown;
        }

        private bool isServerStopped = false;

        private async Task ReceiveDataAsync(int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            var networkStream = tcpClient.GetStream();
            StringBuilder receivedMessageBuilder = new StringBuilder();

            try
            {
                while (!isServerStopped)
                {
                    int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);

                    receivedMessageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    if (receivedMessageBuilder.ToString().Contains("Server Stopped@"))
                    {
                        Stop();
                        isServerStopped = true;
                        break;
                    }

                    if (receivedMessageBuilder.ToString().EndsWith("@"))
                    {
                        string receivedMessage = receivedMessageBuilder.ToString().TrimEnd('@');
                        AddMessage(">> " + receivedMessage);
                        receivedMessageBuilder.Clear();
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException socketEx && (socketEx.SocketErrorCode == SocketError.ConnectionReset || socketEx.SocketErrorCode == SocketError.Disconnecting))
            {
                AddMessage($"Server disconnected unexpectedly");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error receiving message: {ex.Message}");
                await DisconnectAsync();
            }
        }


        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (listChats.Items.Count >= MAX_ITEMS)
                    listChats.Items.RemoveAt(0);
                listChats.Items.Add(message);
            });
        }

        private async Task ConnectAsync()
        {
            IPAddress ipAddress;
            int port;

            if (!IPAddress.TryParse(txtIPServer.Text, out ipAddress) ||
                !int.TryParse(txtPort.Text, out port) || port <= 0 || port > 65535 ||
                !int.TryParse(txtBufferSize.Text, out bufferSize) || bufferSize <= 0)
            {
                MessageBox.Show("Invalid input.");
                return;
            }

           
            if (string.IsNullOrWhiteSpace(txtNameClient.Text))
            {
                MessageBox.Show("Please enter your username.");
                return;
            }

            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(ipAddress, port);
                networkStream = tcpClient.GetStream();

                if (!tcpClient.Connected)
                {
                    MessageBox.Show("Failed to connect to server.");
                    return;
                }

                isConnected = true; 
                btnConnect.Content = "Disconnect";

                AddMessage("Connected!");
                Console.WriteLine("Connected to server!");

              
                username = txtNameClient.Text.Trim();

              
                byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
                await networkStream.WriteAsync(usernameBytes, 0, usernameBytes.Length);

               
                await Task.Run(() => ReceiveDataAsync(bufferSize));
            }
            catch (ObjectDisposedException ex)
            {
              
                MessageBox.Show($"Error connecting");

               
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server");
            }
        }


        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                await ConnectAsync();
            }
            else
            {
                await DisconnectAsync();
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = txtMessage.Text;

            if (!isConnected)
            {
                MessageBox.Show("Not connected to server.");
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Please enter a message to send.");
                return; 
            }

            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] terminatorBytes = Encoding.UTF8.GetBytes("@");

           
                byte[] buffer = new byte[messageBytes.Length + terminatorBytes.Length];

            
                Buffer.BlockCopy(messageBytes, 0, buffer, 0, messageBytes.Length);

               
                Buffer.BlockCopy(terminatorBytes, 0, buffer, messageBytes.Length, terminatorBytes.Length);

                await networkStream.WriteAsync(buffer, 0, buffer.Length);

              
                AddMessage("<< " + message);

              
                txtMessage.Clear();
                txtMessage.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message");
                await DisconnectAsync();
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {

                string disconnectMessage = " disconnected@";
                byte[] byeMessage = Encoding.UTF8.GetBytes(disconnectMessage);

                await networkStream.WriteAsync(byeMessage, 0, byeMessage.Length);
                btnConnect.Content = "Connect";
                isConnected = false; 
                networkStream.Close();
                tcpClient.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting");
            }
        }

        private void Stop()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    AddMessage("server stopped");
                    btnConnect.Content = "Connect";
                    isConnected = false;
                    networkStream.Close();
                    tcpClient.Close();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting: {ex.Message}");
            }
        }


        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                btnSend_Click(sender, e);
            }
        }
    }
}
