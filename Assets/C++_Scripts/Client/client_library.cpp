#include "Client.h"

using namespace Networking;


extern "C" int GetTwo()
{
	return 2;
}

extern "C" Client * CreateClient()
{
	printf("Created Client()\n");
	return new Client();
}

extern "C" void DisposeClient(Client* client)
{
	if(client != NULL)
	{
		delete client;
		client = NULL;
	}
}

extern "C" int Call_Init_TCP_Client_Socket(Client * client, const char * name, short port)
{
	if(client != NULL)
		client->Init_TCP_Client_Socket(name, port);
    return -1;
}

extern "C" void Start_Read_Process(Client * client)
{
		if(fork() == 0)
		{
				int bytesRead;
				while(1)
				{
						char buf[client->PACKETLEN];
						client->Recv(buf, client->PACKETLEN, &bytesRead);
				}
		}
}

extern "C" char * GetData(Client * client)
{
		return client->GetData();
}


extern "C" int Call_Send(Client * client, char * message, int size)
{
	return client->Send(message, size);
}

extern "C" int Call_Recv(Client * client, char * message, int size, int * recvbytes)
{
	return client->Recv(message, size, recvbytes);
}
