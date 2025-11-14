# App for backing up Telegram channels

### Features
- Saves all posts: text, photos, media
  - Every post is placed in separate folder
  - Every post folder contains full metadata
  - Posts folders are organized by date
  - Multiple media posts are saved in one folder 
- If a discussion group is linked to channel, all comments are saved too
- Supports dry-run, reconcile of saved backup, custom post offset

### Requirements
.NET 9 or higher

### Build
1. Clone the repo: `git clone https://github.com/kiraventom/TgChannelBackup`
2. Open the executable dir: `cd ./TgChannelBackup/TgChannelBackup.Cli`
3. Restore the dependencies: `dotnet restore`
4. Build the project: `dotnet build`

### Run
1. Create an app on https://my.telegram.org
2. Create an .env file setting the following environment variables:
    - `TG_API_ID=<App api_id from my.telegram.org>`
    - `TG_API_HASH=<App api_hash from my.telegram.org>`
    - `TG_PHONE=<Phone number of the account you logged in with on my.telegram.org>`
    - `TG_PASSWORD=<Password to the account you logged in with on my.telegram.org. Required only if you have 2FA enabled>`
3. TgChannelBackup takes multiple parameters, two of them are mandatory: `--channel` and `--target`.
    - `--channel` takes `channel_id` of the channel you need to back up. Example: `--channel 1006503122`
    - `--target` takes path to the directory. TgChannelBackup will back up the channel into the "channel_<channel_id>" directory inside of the target one.
4. Run the application. Example: `dotnet run -- --channel 1006503122 --target ~/backup`
    - On the first login, TgChannelBackup will ask for 5-digit code Telegram sends to your other device.
5. The application will start the backup: first the channel posts, then the discussion group.
    - The process can be interrupted with Ctrl+C. TgChannelBackup will pick up from where it has stopped.
    - After completing the backup TgChannelBackup will close itself.

### Generated files
TgChannelBackup stores its database and logs at `~/.local/share/TgChannelBackup` on Linux and at `%APPDATA%/TgChannelBackup` on Windows. 

Database contains the id of last saved post/comment for each channel.

### Bugs
There are some, for sure.
