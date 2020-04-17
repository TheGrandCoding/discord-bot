echo Deploying

# openssl aes-256-cbc -K $encrypted_f4eabd9cea96_key -iv $encrypted_f4eabd9cea96_iv -in .travis/deploy_key.pem.enc -out .travis/deploy_key.pem -d
# eval "$(ssh-agent -s)" #start the ssh agent
# chmod 600 .travis/deploy_key.pem # this key should have push access
# ssh-add .travis/deploy_key.pem
# cd DiscordBot/bin/Release/netcoreapp3.1/linux-arm
# git init
# echo "*.*" >> .gitignore
# echo "!.gitignore" >> .gitignore
# echo "!*/*" >> .gitignore
# echo "DiscordBot" >> .gitignore
# echo "createdump" >> .gitignore
# git add .
# git commit -a -m "Automatic forcepush of build"
# git remote add origin git@github.com:CheAle14/bot-binary.git
# git push origin HEAD:master --force