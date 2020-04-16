using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessClient.Classes
{
    public class Packet
    {
        public PacketId Id { get; set; }
        public JObject Content { get; set; }
        public Packet(PacketId id, JObject content)
        {
            Id = id;
            Content = content;
        }

        public override string ToString()
        {
            var jobj = new JObject();
            jobj["id"] = Id.ToString();
            jobj["content"] = Content.ToString();
            return jobj.ToString();
        }
    }

    public enum PacketId
    {
        #region Client -> Server Messages
        /// <summary>
        /// Initial connection request
        /// Content: <see cref="string"/>[] of Token and join/spectate
        /// </summary>
        ConnRequest,

        /// <summary>
        /// Request that we move a piece
        /// Content: <see cref="string"/> 'from', and 'to'
        /// </summary>
        MoveRequest,

        /// <summary>
        /// Client indicates they are resigning
        /// </summary>
        ResignRequest,

        /// <summary>
        /// Requests full information of a <seealso cref="ChessPlayer"/>
        /// Content: <see cref="int"/> Id
        /// </summary>
        IdentRequest,

        /// <summary>
        /// Admin asks Server to demand a screenshot.
        /// Content: Id of player.
        /// </summary>
        RequestScreen,

        /// <summary>
        /// Admin asks that the previous player's move be undone, and it be their turn again.
        /// </summary>
        RequestRevertMove,

        /// <summary>
        /// Admin asks Server to demand list of processes.
        /// Content: Id of player.
        /// </summary>
        RequestProcesses,

        /// <summary>
        /// Client requests the game be ended
        /// Can be used by both players and admins.
        /// Content: Id of winner
        /// </summary>
        RequestGameEnd,

        /// <summary>
        /// Client indicates they errored for logging purposes
        /// Content: Information about the error.
        /// </summary>
        Errored,

        #endregion

        #region Server -> Client Messages
        /// <summary>
        /// Sends the current status of the game.
        /// Content: Game content
        /// </summary>
        GameStatus,

        /// <summary>
        /// Sends full information of a player.
        /// Content: ChessPlayer
        /// </summary>
        PlayerIdent,

        /// <summary>
        /// Informs Client to move a piece to a location
        /// Content: <see cref="string"/> From, To.
        /// Content: Optionally: Remove, int[] of Piece Ids to remove from board as though they were taken
        /// </summary>
        MoveMade,

        /// <summary>
        /// Informs Client that the given move has been reverted.
        /// Content: <see cref="Move"/>
        /// </summary>
        MoveReverted,

        /// <summary>
        /// Orders the Client to reply how they are to promote a pawn
        /// Content: <see cref="string"/> Location
        /// </summary>
        ChoosePromotion,

        /// <summary>
        /// Orders Client to reflect an update to a Piece,
        /// Content: Full Chess piece
        /// </summary>
        PieceUpdated,

        /// <summary>
        /// Orders Client to reflec an update to a Location
        /// Content: Full ChessLocation
        /// </summary>
        LocationUpdated,

        /// <summary>
        /// Informs Clients that a new Player has joined
        /// Content: Full ChessPlayer, and mode
        /// </summary>
        ConnectionMade,

        /// <summary>
        /// Informs Clients when a user has disconnected
        /// Content: player Id
        /// </summary>
        UserDisconnected,

        /// <summary>
        /// Informs Client that they are an admin and are entitled to open the admin form.
        /// </summary>
        NotifyAdmin,

        /// <summary>
        /// Informs Client to screenshot and upload their current screen
        /// </summary>
        DemandScreen,

        /// <summary>
        /// Informs Client to perform a PUT request with their current running processes.
        /// </summary>
        DemandProcesses,

        /// <summary>
        /// Informs Clients that the game has ended
        /// Content: Id of winner.
        /// </summary>
        GameEnd,

        /// <summary>
        /// Informs Client that their move request was invalid
        /// Content: string Title, string Message
        /// </summary>
        MoveRequestRefuse,

        #endregion

        #region Multipurpose Messages

        #endregion
    }
}
