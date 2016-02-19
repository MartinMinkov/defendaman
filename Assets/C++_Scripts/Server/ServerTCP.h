#ifndef SERVER_TCP
#define SERVER_TCP
#include "Server.h"

namespace Networking
{
	class ServerTCP : public Server
	{
		public:
			ServerTCP(){}
			~ServerTCP(){}
			/*
	            Initialize socket, server address to lookup to, and connect to the server

	            @return: socket file descriptor
            */
            int InitializeSocket(short port);

            /*
	            Initialize socket, server address to lookup to, and connect to the server

	            @return: socket file descriptor
            */
            int Accept(Player * player);


            int CreateClientManager(int PlayerID);
            /*
	            Recives packets from a specific socket, should be in a child proccess

	            @return: packet of size PACKETLEN
            */
            int Receive(Player * player);

	        /*
                Sends a message to all the clients

            */
            virtual void Broadcast(char* message);

            void PrintPlayer(Player p);
        private:

            /* Team ONE - Player connections and info. */
            std::vector<Player>             _PlayerList;
	};
}

#endif