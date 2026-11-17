import * as signalR from '@microsoft/signalr';

let connection: signalR.HubConnection | null = null;

const getHubUrl = () => {
  return '/hubs/queue';
};

export const getSignalRConnection = (): signalR.HubConnection => {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => localStorage.getItem('queueless_token') || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }
  return connection;
};

export const startSignalR = async (): Promise<signalR.HubConnection> => {
  const conn = getSignalRConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    try {
      await conn.start();
      console.log('SignalR connected successfully.');
    } catch (err) {
      console.error('SignalR connection failed:', err);
    }
  }
  return conn;
};
